using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.MasterDatas;
using HC.Permissions;
using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.DocumentAssignments.Default)]
public class WorkflowActionService : HCAppService, IWorkflowActionService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly DocumentAssignmentManager _documentAssignmentManager;
    private readonly DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowTemplate, Guid> _workflowTemplateRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IWorkflowSlaService _workflowSlaService;
    private readonly IWorkflowDocumentFileService _workflowDocumentFileService;
    private readonly IWorkflowNotificationService _workflowNotificationService;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;
    private readonly IParallelSigningMergeService _parallelSigningMergeService;
    private readonly IReadOnlyList<IWorkflowSigningStrategy> _workflowSigningStrategies;

    public WorkflowActionService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentAssignmentManager documentAssignmentManager,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        IWorkflowSlaService workflowSlaService,
        IWorkflowDocumentFileService workflowDocumentFileService,
        IWorkflowNotificationService workflowNotificationService,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService,
        IParallelSigningMergeService parallelSigningMergeService,
        IEnumerable<IWorkflowSigningStrategy> workflowSigningStrategies)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _masterDataRepository = masterDataRepository;
        _workflowSlaService = workflowSlaService;
        _workflowDocumentFileService = workflowDocumentFileService;
        _workflowNotificationService = workflowNotificationService;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
        _parallelSigningMergeService = parallelSigningMergeService;
        _workflowSigningStrategies = workflowSigningStrategies.ToList();
    }

    public async Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.DocumentWorkflowInstanceId);
        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
            && instance.Status != nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            if (!instance.OverdueAt.HasValue
                || Clock.Now >= BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt.Value))
            {
                throw new Volo.Abp.UserFriendlyException(L["WorkflowOverdueGraceExpired"]);
            }
        }

        var assignment = await _documentAssignmentRepository.GetAsync(input.DocumentAssignmentId);
        if (assignment.Status != nameof(DocumentAssignmentStatus.PENDING))
        {
            throw new Volo.Abp.UserFriendlyException(L["AssignmentNotPending"]);
        }

        if (assignment.ReceiverUserId != CurrentUser.Id!.Value)
        {
            throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        if (assignment.DocumentId != instance.DocumentId)
        {
            throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowAction"]);
        }

        var now = Clock.Now;

        if (input.Action.ToUpper() == nameof(WorkflowInstanceLogAction.APPROVE)
            && !WorkflowStepNavigationHelper.IsViewStep(assignment.ActionType))
        {
            await ApplySigningByMethodAsync(assignment, instance, input.SigningMethodId, input.Note, input.UserSignatureId);
        }

        switch (input.Action.ToUpper())
        {
            case nameof(WorkflowInstanceLogAction.APPROVE):
                await HandleApproveAsync(instance, assignment, now, input.Note, input.NextStepSignerUserId);
                break;
            case nameof(WorkflowInstanceLogAction.RETURN):
                await HandleReturnAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.REJECT):
                await HandleTerminalActionAsync(instance, assignment, now, input.Note,
                    nameof(DocumentWorkflowInstanceStatus.REJECTED),
                    nameof(WorkflowInstanceLogAction.REJECT),
                    "WorkflowRejected", "WorkflowRejectedMessage",
                    DocumentStatusCode.TU_CHOI);
                break;
            default:
                throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowAction"]);
        }

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(instance);
    }

    private async Task HandleApproveAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note,
        Guid? nextStepSignerUserId = null)
    {
        assignment.Status = nameof(DocumentAssignmentStatus.DONE);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        var template = await _workflowTemplateRepository.GetAsync(instance.WorkflowTemplateId);
        var isParallel = template.SignMode == nameof(SignMode.PARALLEL);

        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        var sameStepPending = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.WorkflowStepTemplateId == assignment.WorkflowStepTemplateId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING)
            && x.Id != assignment.Id);

        foreach (var other in sameStepPending)
        {
            other.Status = nameof(DocumentAssignmentStatus.REVOKE);
            other.ProcessedAt = now;
            other.IsCurrent = false;
            await _documentAssignmentRepository.UpdateAsync(other);
            Logger.LogInformation(
                "[APPROVE] Revoked same-step assignment {AssignmentId} for user {UserId} (secondary user auto-revoke)",
                other.Id, other.ReceiverUserId);
        }

        var remainingPending = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING));

        if (remainingPending.Any())
        {
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

            await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            return;
        }

        var freshInstance = await _documentWorkflowInstanceRepository.GetAsync(instance.Id);
        if (freshInstance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            Logger.LogWarning(
                "[RACE_GUARD] Workflow {InstanceId} already transitioned to {Status} by another thread. " +
                "Current user {UserId} approve logged but completion skipped.",
                instance.Id, freshInstance.Status, CurrentUser.Id);

            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                freshInstance.Status,
                freshInstance.Status,
                $"[RACE_GUARD] Workflow already {freshInstance.Status}. {note}");
            return;
        }

        instance = freshInstance;

        if (isParallel)
        {
            await HandleParallelCompleteAsync(instance, assignment, currentStep, now, note);
        }
        else
        {
            var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);

            var currentIndex = allSteps.FindIndex(s => s.Id == currentStep.Id);
            var nextBlockingIndex = WorkflowStepNavigationHelper.AdvanceThroughViewSteps(
                instance, allSteps, currentIndex + 1);

            if (nextBlockingIndex >= allSteps.Count)
            {
                instance.Status = nameof(DocumentWorkflowInstanceStatus.COMPLETED);
                instance.FinishedAt = now;
                await _documentWorkflowInstanceRepository.UpdateAsync(instance);

                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id, assignment.Id, CurrentUser.Id,
                    nameof(WorkflowInstanceLogAction.APPROVE),
                    currentStep.Type,
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    nameof(DocumentWorkflowInstanceStatus.COMPLETED), note);

                var document = await _documentRepository.GetAsync(instance.DocumentId);
                await _workflowNotificationService.SendWorkflowNotificationAsync(
                    document,
                    new List<Guid> { instance.CreatorId!.Value },
                    "WorkflowCompleted",
                    $"WorkflowCompletedMessage|{document.StorageNumber}|{document.Title}");

                await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.HT);
            }
            else
            {
                var nextStep = allSteps[nextBlockingIndex];
                if (!WorkflowStepNavigationHelper.IsBlockingStep(nextStep.Type))
                {
                    throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowAction"]);
                }

                instance.CurrentStepId = nextStep.Id;
                instance.FinishedAt = _workflowSlaService.CalculateStepDeadline(now, nextStep.SLADays);
                instance.OverdueAt = null;

                await _documentWorkflowInstanceRepository.UpdateAsync(instance);

                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id, assignment.Id, CurrentUser.Id,
                    nameof(WorkflowInstanceLogAction.APPROVE),
                    currentStep.Type,
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

                var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
                    x => x.StepId == nextStep.Id && x.IsActive);

                var nextStepFileId = await _workflowDocumentFileService.CopyDocumentFileForNextStepAsync(
                    assignment.DocumentFileResultId, instance.DocumentId);

                var document = await _documentRepository.GetAsync(instance.DocumentId);
                var nextUserIds = new List<Guid>();

                var nextStepDetail = await _workflowSubmitInfoQueryService.BuildWorkflowStepDetailAsync(
                    nextStep,
                    stepAssignments,
                    instance.CreatorId!.Value);

                List<WorkflowStepUserDto> nextReceivers;

                if (nextStepSignerUserId.HasValue)
                {
                    var explicitSelection = nextStepDetail.CandidateUsers.FirstOrDefault(
                        c => c.UserId == nextStepSignerUserId.Value);
                    if (explicitSelection == null)
                    {
                        throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
                    }

                    WorkflowSubmissionHelper.SetSelectedSignerForStep(instance, nextStep.Id, nextStepSignerUserId.Value);
                    await _documentWorkflowInstanceRepository.UpdateAsync(instance, autoSave: true);
                }

                var preselectedSignerUserId = WorkflowSubmissionHelper.GetSelectedSignerForStep(instance, nextStep.Id);
                if (nextStepDetail.CandidateUsers.Count <= 1)
                {
                    nextReceivers = nextStepDetail.CandidateUsers;
                }
                else if (preselectedSignerUserId.HasValue)
                {
                    var selected = nextStepDetail.CandidateUsers.FirstOrDefault(
                        c => c.UserId == preselectedSignerUserId.Value);
                    if (selected == null)
                    {
                        throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
                    }

                    nextReceivers = new List<WorkflowStepUserDto> { selected };
                }
                else
                {
                    throw new Volo.Abp.UserFriendlyException(L["WorkflowSignerSelectionRequired"]);
                }

                var nextSigningPlaceholderIndex = WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(
                    allSteps,
                    nextStep.Id);
                foreach (var receiver in nextReceivers)
                {
                    await _documentAssignmentManager.CreateAsync(
                        instance.DocumentId,
                        nextStep.Id,
                        receiver.UserId,
                        nextSigningPlaceholderIndex,
                        nextStep.Type,
                        nameof(DocumentAssignmentStatus.PENDING),
                        now,
                        DateTime.MinValue,
                        true,
                        nextStepFileId);
                    nextUserIds.Add(receiver.UserId);
                }

                if (nextUserIds.Any())
                {
                    var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);
                    await _workflowNotificationService.SendWorkflowNotificationAsync(
                        document,
                        nextUserIds,
                        "WorkflowAssigned",
                        $"WorkflowAssignedMessage|{document.StorageNumber}|{document.Title}|{workflow.Name}|{nextStep.Name}");
                }

                await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            }
        }
    }

    private async Task HandleParallelCompleteAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment triggeringAssignment,
        WorkflowStepTemplate currentStep,
        DateTime now,
        string? note)
    {
        Logger.LogInformation(
            "[PARALLEL_COMPLETE] All parallel assignments done. Starting merge for instance {InstanceId}",
            instance.Id);

        try
        {
            await _parallelSigningMergeService.MergeSignedPdfsForParallelAsync(instance);
        }
        catch (Volo.Abp.UserFriendlyException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "[PARALLEL_COMPLETE] Error merging signed PDFs for instance {InstanceId}",
                instance.Id);
            throw new Volo.Abp.UserFriendlyException(L["ParallelSigningMergeFailed"]);
        }

        instance.Status = nameof(DocumentWorkflowInstanceStatus.COMPLETED);
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, triggeringAssignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.APPROVE),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.COMPLETED),
            $"PARALLEL workflow completed - all steps done. {note}");

        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await _workflowNotificationService.SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowCompleted",
            $"WorkflowCompletedMessage|{document.StorageNumber}|{document.Title}");

        await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.HT);

        Logger.LogInformation(
            "[PARALLEL_COMPLETE] Parallel workflow completed successfully. InstanceId={InstanceId}",
            instance.Id);
    }

    private async Task HandleTerminalActionAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note,
        string newInstanceStatus,
        string logAction,
        string notificationTitleKey,
        string notificationMessageKey,
        DocumentStatusCode documentStatusCode)
    {
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING)
            && x.Id != assignment.Id);

        foreach (var other in otherPendingAssignments)
        {
            other.Status = nameof(DocumentAssignmentStatus.REVOKE);
            other.ProcessedAt = now;
            other.IsCurrent = false;
            await _documentAssignmentRepository.UpdateAsync(other);
        }

        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        instance.Status = newInstanceStatus;
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            logAction,
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            newInstanceStatus, note);

        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await _workflowNotificationService.SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            notificationTitleKey,
            $"{notificationMessageKey}|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? WorkflowConstants.RoleSystem}");

        await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, documentStatusCode);
    }

    private async Task HandleReturnAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note)
    {
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        var template = await _workflowTemplateRepository.GetAsync(instance.WorkflowTemplateId);
        var isParallel = template.SignMode == nameof(SignMode.PARALLEL);

        var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING)
            && x.Id != assignment.Id);

        if (!isParallel)
        {
            otherPendingAssignments = otherPendingAssignments
                .Where(x => x.StepOrder == assignment.StepOrder).ToList();
        }

        foreach (var other in otherPendingAssignments)
        {
            other.Status = nameof(DocumentAssignmentStatus.REVOKE);
            other.ProcessedAt = now;
            other.IsCurrent = false;
            await _documentAssignmentRepository.UpdateAsync(other);
        }

        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        if (!allSteps.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        var firstStep = allSteps[0];

        instance.Status = nameof(DocumentWorkflowInstanceStatus.RETURNED);
        instance.CurrentStepId = firstStep.Id;
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.RETURN),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.RETURNED), note);

        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await _workflowNotificationService.SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowReturned",
            $"WorkflowReturnedMessage|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? WorkflowConstants.RoleSystem}");

        await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.TRA_VE);
    }

    private async Task ApplySigningByMethodAsync(
        DocumentAssignment assignment,
        DocumentWorkflowInstance instance,
        Guid? signingMethodId,
        string? noteContent,
        Guid? selectedUserSignatureId)
    {
        if (!signingMethodId.HasValue)
        {
            return;
        }

        var signingMethod = await _masterDataRepository.FindAsync(signingMethodId.Value);
        Logger.LogInformation(
            "[SIGN_DISPATCH] SigningMethodId={SigningMethodId}, MethodCode={MethodCode}",
            signingMethodId,
            signingMethod?.Code);

        if (signingMethod == null || string.IsNullOrWhiteSpace(signingMethod.Code))
        {
            return;
        }

        var strategy = _workflowSigningStrategies
            .FirstOrDefault(x => x.MethodCode == signingMethod.Code);
        if (strategy != null)
        {
            await strategy.ApplyAsync(assignment, instance, noteContent, selectedUserSignatureId);
        }
    }
}
