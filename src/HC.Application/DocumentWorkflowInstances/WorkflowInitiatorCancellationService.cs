using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.Permissions;
using HC.WorkflowStepTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.Documents.SubmitForSigning)]
public class WorkflowInitiatorCancellationService : HCAppService, IWorkflowInitiatorCancellationService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly WorkflowInstanceCancellationService _workflowInstanceCancellationService;

    public WorkflowInitiatorCancellationService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        WorkflowInstanceCancellationService workflowInstanceCancellationService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _documentFileRepository = documentFileRepository;
        _workflowInstanceCancellationService = workflowInstanceCancellationService;
    }

    public async Task CancelWorkflowByInitiatorAsync(CancelWorkflowByInitiatorInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        var currentUserId = CurrentUser.Id
            ?? throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]);

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);

        if (instance.CreatorId != currentUserId)
        {
            throw new Volo.Abp.UserFriendlyException(L["OnlyInitiatorCanCancelWorkflow"]);
        }

        var cancellableStatuses = new[]
        {
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.OVERDUE)
        };

        if (!cancellableStatuses.Contains(instance.Status))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotCancellable"]);
        }

        var assignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId && x.WorkflowStepTemplateId != null);

        var stepTemplates = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        var stepDict = stepTemplates.ToDictionary(s => s.Id, s => s);

        var documentFiles = await _documentFileRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId);

        if (WorkflowSigningProgressHelper.HasWorkflowSigningOccurred(
                instance, assignments, stepDict, documentFiles))
        {
            throw new Volo.Abp.UserFriendlyException(L["CannotCancelWorkflowAfterSigning"]);
        }

        var historyNote = string.IsNullOrWhiteSpace(input.Reason)
            ? L["WorkflowCancelledByInitiatorDefaultNote"]
            : input.Reason.Trim();
        var logNote = historyNote;
        var fromStatus = instance.Status;

        await _workflowInstanceCancellationService.CancelInstanceAsync(
            instance,
            Clock.Now,
            currentUserId,
            historyNote,
            logNote,
            fromStatus,
            WorkflowConstants.RoleInitiator,
            Logger);

        Logger.LogInformation(
            "[WORKFLOW_CANCEL] Initiator {UserId} cancelled instance {InstanceId}",
            currentUserId,
            instance.Id);
    }
}
