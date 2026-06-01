using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.Permissions;
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
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;
    private readonly IWorkflowInstanceQueryService _workflowInstanceQueryService;
    private readonly IWorkflowNotificationService _workflowNotificationService;

    public WorkflowSignerManagementService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentAssignmentManager documentAssignmentManager,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService,
        IWorkflowInstanceQueryService workflowInstanceQueryService,
        IWorkflowNotificationService workflowNotificationService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _identityUserRepository = identityUserRepository;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
        _workflowInstanceQueryService = workflowInstanceQueryService;
        _workflowNotificationService = workflowNotificationService;
    }

    public async Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        if (instance.CreatorId != CurrentUser.Id)
        {
            throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId && x.CreationTime >= instance.StartedAt);

        var stepsStatus = await _workflowInstanceQueryService.GetAllStepsWithStatusAsync(input.WorkflowInstanceId);
        var editableSteps = stepsStatus.Where(s => s.CanEditSigner).ToDictionary(s => s.StepId);

        if (!input.StepSignerSelections.Any())
        {
            return;
        }

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
            if (!editableSteps.TryGetValue(selection.StepId, out var stepStatus))
            {
                throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
            }

            if (!stepStatus.CandidateUsers.Any(c => c.UserId == selection.SelectedUserId))
            {
                throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
            }

            if (stepStatus.CurrentPendingReceiverUserId == selection.SelectedUserId)
            {
                continue;
            }

            var step = allSteps.First(s => s.Id == selection.StepId);
            var pendingOnStep = docAssignments
                .Where(a => a.WorkflowStepTemplateId == step.Id
                    && a.Status == nameof(DocumentAssignmentStatus.PENDING)
                    && a.IsCurrent)
                .ToList();

            if (!pendingOnStep.Any())
            {
                throw new Volo.Abp.UserFriendlyException(L["WorkflowStepSignerNotEditable"]);
            }

            var sourceAssignment = pendingOnStep.First();
            var stepFileId = sourceAssignment.DocumentFileResultId;

            foreach (var pending in pendingOnStep)
            {
                pending.Status = nameof(DocumentAssignmentStatus.REVOKE);
                pending.ProcessedAt = now;
                pending.IsCurrent = false;
            }

            await _documentAssignmentRepository.UpdateManyAsync(pendingOnStep);

            await _documentAssignmentManager.CreateAsync(
                instance.DocumentId,
                step.Id,
                selection.SelectedUserId,
                step.Order,
                step.Type,
                nameof(DocumentAssignmentStatus.PENDING),
                now,
                DateTime.MinValue,
                true,
                stepFileId);

            notifyUserIds.Add(selection.SelectedUserId);

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
        }

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
}
