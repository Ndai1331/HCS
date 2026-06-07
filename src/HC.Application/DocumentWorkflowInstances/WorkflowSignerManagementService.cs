using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.Permissions;
using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;
using HC.Workflows;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.Documents.SubmitForSigning)]
public class WorkflowSignerManagementService : HCAppService, IWorkflowSignerManagementService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly DocumentAssignmentManager _documentAssignmentManager;
    private readonly DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowNotificationService _workflowNotificationService;

    public WorkflowSignerManagementService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentAssignmentManager documentAssignmentManager,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowNotificationService workflowNotificationService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _identityUserRepository = identityUserRepository;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowNotificationService = workflowNotificationService;
    }

    public async Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        var stepIds = allSteps.Select(s => s.Id).ToList();
        var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && stepIds.Contains(x.StepId.Value) && x.IsActive);
        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId && x.CreationTime >= instance.StartedAt);

        if (!CanCurrentUserEditSigners(instance, allSteps, docAssignments))
        {
            throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        if (!input.StepSignerSelections.Any())
        {
            return;
        }

        var selectionStepIds = input.StepSignerSelections.Select(s => s.StepId).Distinct().ToList();
        var editableStepMap = await BuildEditableStepSignerMapAsync(
            instance, allSteps, docAssignments, stepAssignments, selectionStepIds);

        var now = Clock.Now;
        var notifyUserIds = new List<Guid>();
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);

        var signerUserIds = input.StepSignerSelections
            .Select(s => s.SelectedUserId)
            .Concat(
                docAssignments
                    .Where(a => a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                    .Select(a => a.ReceiverUserId))
            .Distinct()
            .ToList();
        var signerUsers = signerUserIds.Any()
            ? await _identityUserRepository.GetListAsync(x => signerUserIds.Contains(x.Id))
            : new List<IdentityUser>();
        var signerUserDict = signerUsers.ToDictionary(u => u.Id);

        foreach (var selection in input.StepSignerSelections)
        {
            if (!editableStepMap.TryGetValue(selection.StepId, out var stepEditInfo))
            {
                throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
            }

            if (!stepEditInfo.CandidateUsers.Any(c => c.UserId == selection.SelectedUserId))
            {
                throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
            }

            if (stepEditInfo.CurrentReceiverUserId == selection.SelectedUserId)
            {
                continue;
            }

            var step = allSteps.First(s => s.Id == selection.StepId);
            var pendingOnStep = docAssignments
                .Where(a => a.WorkflowStepTemplateId == step.Id
                    && a.Status == nameof(DocumentAssignmentStatus.PENDING)
                    && a.IsCurrent)
                .ToList();

            DocumentAssignment? sourceAssignment = null;
            if (pendingOnStep.Any())
            {
                sourceAssignment = pendingOnStep.First();
                var stepFileId = sourceAssignment.DocumentFileResultId;

                foreach (var pending in pendingOnStep)
                {
                    pending.Status = nameof(DocumentAssignmentStatus.REVOKE);
                    pending.ProcessedAt = now;
                    pending.IsCurrent = false;
                }

                await _documentAssignmentRepository.UpdateManyAsync(pendingOnStep);

                var allCommittedSteps = await _workflowCommittedStepsQueryService
                    .LoadCommittedWorkflowStepsOrderedAsync(instance);
                var signingPlaceholderIndex = WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(
                    allCommittedSteps,
                    step.Id);

                await _documentAssignmentManager.CreateAsync(
                    instance.DocumentId,
                    step.Id,
                    selection.SelectedUserId,
                    signingPlaceholderIndex,
                    step.Type,
                    nameof(DocumentAssignmentStatus.PENDING),
                    now,
                    DateTime.MinValue,
                    true,
                    stepFileId);

                notifyUserIds.Add(selection.SelectedUserId);
            }

            WorkflowSubmissionHelper.SetSelectedSignerForStep(instance, step.Id, selection.SelectedUserId);

            if (sourceAssignment != null)
            {
                signerUserDict.TryGetValue(sourceAssignment.ReceiverUserId, out var fromUser);
                signerUserDict.TryGetValue(selection.SelectedUserId, out var toUser);
                var updateSignerNote = L["WorkflowLogUpdateSignerDetail", step.Order, step.Name,
                    WorkflowInstanceLogHelper.FormatIdentityUserDisplayName(fromUser),
                    WorkflowInstanceLogHelper.FormatIdentityUserDisplayName(toUser)];

                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id,
                    sourceAssignment.Id,
                    CurrentUser.Id,
                    nameof(WorkflowInstanceLogAction.UPDATE_SIGNER),
                    step.Type,
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    updateSignerNote);
                continue;
            }

            var previousDisplay = stepEditInfo.CurrentReceiverUserId.HasValue
                ? signerUserDict.TryGetValue(stepEditInfo.CurrentReceiverUserId.Value, out var previousUser)
                    ? WorkflowInstanceLogHelper.FormatIdentityUserDisplayName(previousUser)
                    : "---"
                : "---";
            signerUserDict.TryGetValue(selection.SelectedUserId, out var preselectedToUser);
            var preselectedUpdateSignerNote = L["WorkflowLogUpdateSignerDetail", step.Order, step.Name,
                previousDisplay,
                WorkflowInstanceLogHelper.FormatIdentityUserDisplayName(preselectedToUser)];

            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id,
                null,
                CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.UPDATE_SIGNER),
                step.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                preselectedUpdateSignerNote);
        }

        await _documentWorkflowInstanceRepository.UpdateAsync(instance, autoSave: true);

        if (notifyUserIds.Any())
        {
            var distinctNotify = notifyUserIds.Distinct().ToList();
            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            await _workflowNotificationService.SendWorkflowNotificationAsync(
                document,
                distinctNotify,
                "WorkflowAssigned",
                $"WorkflowAssignedMessage|{document.StorageNumber}|{document.Title}|{workflow.Name}|{currentStep.Name}");
        }
    }

    private async Task<Dictionary<Guid, EditableStepSignerInfo>> BuildEditableStepSignerMapAsync(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepTemplate> allSteps,
        IReadOnlyList<DocumentAssignment> docAssignments,
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        IReadOnlyList<Guid> stepIdsToValidate)
    {
        var submitterUserId = instance.CreatorId ?? CurrentUser.Id
            ?? throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
        var result = new Dictionary<Guid, EditableStepSignerInfo>();

        foreach (var stepId in stepIdsToValidate)
        {
            var step = allSteps.FirstOrDefault(s => s.Id == stepId);
            if (step == null)
            {
                continue;
            }

            var thisStepDocAssignments = docAssignments.Where(a => a.WorkflowStepTemplateId == step.Id).ToList();
            var isCompleted = thisStepDocAssignments.Any(a => a.Status == nameof(DocumentAssignmentStatus.DONE));
            var pendingAssignments = thisStepDocAssignments
                .Where(a => a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                .ToList();

            if (isCompleted)
            {
                continue;
            }

            var templateAssignments = stepAssignments.Where(sa => sa.StepId == step.Id).ToList();
            var stepDetail = await _workflowSubmitInfoQueryService.BuildWorkflowStepDetailAsync(
                step, templateAssignments, submitterUserId);
            if (!stepDetail.CandidateUsers.Any())
            {
                continue;
            }

            var selectedBySubmit = WorkflowSubmissionHelper.GetSelectedSignerForStep(instance, step.Id);
            var currentReceiver = pendingAssignments.FirstOrDefault()?.ReceiverUserId ?? selectedBySubmit;

            result[stepId] = new EditableStepSignerInfo(
                currentReceiver,
                stepDetail.CandidateUsers);
        }

        return result;
    }

    private bool CanCurrentUserEditSigners(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepTemplate> allSteps,
        IReadOnlyList<DocumentAssignment> docAssignments)
    {
        if (!CurrentUser.Id.HasValue)
        {
            return false;
        }

        if (instance.CreatorId == CurrentUser.Id)
        {
            return true;
        }

        var currentStepId = allSteps.FirstOrDefault(s => s.Id == instance.CurrentStepId)?.Id;
        if (!currentStepId.HasValue)
        {
            return false;
        }

        return docAssignments.Any(a =>
            a.WorkflowStepTemplateId == currentStepId.Value
            && a.Status == nameof(DocumentAssignmentStatus.PENDING)
            && a.IsCurrent
            && a.ReceiverUserId == CurrentUser.Id.Value);
    }

    private sealed record EditableStepSignerInfo(
        Guid? CurrentReceiverUserId,
        List<WorkflowStepUserDto> CandidateUsers);
}
