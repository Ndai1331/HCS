using HC.Shared;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.Documents;
using HC.DocumentAssignments;
using HC.DocumentWorkflowInstanceLogss;
using HC.Notifications;
using HC.NotificationReceivers;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.DocumentWorkflowInstances;
using HC.WorkflowStepAssignments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.EventBus.Distributed;
using Volo.Abp.Identity;
using HC.MasterDatas;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentFiles;
using HC.DocumentHistories;
using Volo.Abp.BlobStoring;
using Microsoft.Extensions.Logging;

namespace HC.DocumentWorkflowInstances;

public class DocumentWorkflowInstancesAppService : DocumentWorkflowInstancesAppServiceBase, IDocumentWorkflowInstancesAppService
{
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly DocumentAssignmentManager _documentAssignmentManager;
    private readonly IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;
    private readonly DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationReceiverRepository _notificationReceiverRepository;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IRepository<DocumentWorkflowInstanceFile, Guid> _documentWorkflowInstanceFileRepository;
    private readonly DocumentManager _documentManager;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly DocumentHistoryManager _documentHistoryManager;
    private readonly IDocumentHistoryRepository _documentHistoryRepository;
    private readonly IBlobContainer _blobContainer;

    public DocumentWorkflowInstancesAppService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        DocumentWorkflowInstanceManager documentWorkflowInstanceManager,
        IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> downloadTokenCache,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentAssignmentManager documentAssignmentManager,
        IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        INotificationRepository notificationRepository,
        INotificationReceiverRepository notificationReceiverRepository,
        IDistributedEventBus distributedEventBus,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        IRepository<DocumentWorkflowInstanceFile, Guid> documentWorkflowInstanceFileRepository,
        DocumentManager documentManager,
        IRepository<DocumentFile, Guid> documentFileRepository,
        DocumentHistoryManager documentHistoryManager,
        IDocumentHistoryRepository documentHistoryRepository,
        IBlobContainer blobContainer
    ) : base(documentWorkflowInstanceRepository, documentWorkflowInstanceManager, downloadTokenCache, documentRepository, workflowRepository, workflowTemplateRepository, workflowStepTemplateRepository)
    {
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _notificationRepository = notificationRepository;
        _notificationReceiverRepository = notificationReceiverRepository;
        _distributedEventBus = distributedEventBus;
        _identityUserRepository = identityUserRepository;
        _masterDataRepository = masterDataRepository;
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentManager = documentManager;
        _documentFileRepository = documentFileRepository;
        _documentHistoryManager = documentHistoryManager;
        _documentHistoryRepository = documentHistoryRepository;
        _blobContainer = blobContainer;
    }

    #region GetWorkflowSubmitInfoAsync

    /// <summary>
    /// Get workflow info (steps, assignments, template) for the submit modal
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId)
    {
        // Get workflow
        var workflow = await _workflowRepository.GetAsync(workflowId);

        // Get active workflow template
        var templates = await _workflowTemplateRepository.GetListAsync(x => x.WorkflowId == workflowId);
        var activeTemplate = templates.FirstOrDefault();
        if (activeTemplate == null)
        {
            throw new UserFriendlyException(L["NoActiveWorkflowTemplateFound"]);
        }

        // Get step templates ordered by Order
        var stepTemplates = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == activeTemplate.Id && x.IsActive);
        stepTemplates = stepTemplates.OrderBy(x => x.Order).ToList();

        if (!stepTemplates.Any())
        {
            throw new UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        // Get step assignments for all steps
        var stepIds = stepTemplates.Select(s => s.Id).ToList();
        var allAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => stepIds.Contains(x.StepId!.Value) && x.IsActive);

        // Get user info for all assigned users
        var userIds = allAssignments.Where(a => a.DefaultUserId.HasValue).Select(a => a.DefaultUserId!.Value).Distinct().ToList();
        var users = userIds.Any()
            ? await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id))
            : new List<IdentityUser>();
        var userDict = users.ToDictionary(u => u.Id, u => u);

        // Build result
        var result = new WorkflowSubmitInfoDto
        {
            WorkflowId = workflow.Id,
            WorkflowName = workflow.Name,
            WorkflowTemplateId = activeTemplate.Id,
            WorkflowTemplateName = activeTemplate.Name,
            WordTemplatePath = activeTemplate.WordTemplatePath,
            HasTemplateFile = !string.IsNullOrWhiteSpace(activeTemplate.WordTemplatePath),
            Steps = stepTemplates.Select(step => new WorkflowStepDetailDto
            {
                StepId = step.Id,
                Order = step.Order,
                Name = step.Name,
                Type = step.Type,
                SLADays = step.SLADays,
                AllowReturn = step.AllowReturn,
                AssignedUsers = allAssignments
                    .Where(a => a.StepId == step.Id && a.DefaultUserId.HasValue)
                    .Select(a => new WorkflowStepUserDto
                    {
                        UserId = a.DefaultUserId!.Value,
                        UserName = userDict.ContainsKey(a.DefaultUserId.Value) ? userDict[a.DefaultUserId.Value].UserName : "Unknown",
                        FullName = userDict.ContainsKey(a.DefaultUserId.Value) ? userDict[a.DefaultUserId.Value].Name : null,
                        IsPrimary = a.IsPrimary
                    }).ToList()
            }).ToList()
        };

        return result;
    }

    #endregion

    #region SubmitToWorkflowAsync

    /// <summary>
    /// Submit a document to a workflow
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
    {
        // Validate
        if (input.WorkflowId == default)
            throw new UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);

        // Get workflow info (needed before document creation for template file path)
        var workflowInfo = await GetWorkflowSubmitInfoAsync(input.WorkflowId);

        if (!workflowInfo.Steps.Any())
        {
            throw new UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        Guid documentId;
        Guid? templateDocumentFileId = null;
        Document? createdDocument = null; // Keep reference to avoid re-fetching before UoW commit

        // If UseWorkflowTemplateFile = true, create a new Document + DocumentFile from the template
        if (input.UseWorkflowTemplateFile)
        {
            if (!workflowInfo.HasTemplateFile || string.IsNullOrWhiteSpace(workflowInfo.WordTemplatePath))
            {
                throw new UserFriendlyException(L["WorkflowTemplateHasNoFile"]);
            }

            // Get default MasterData values for required fields
            var defaultTypeId = await GetDefaultMasterDataIdAsync(MasterDataType.DocumentType);
            var defaultUrgencyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.UrgencyLevel);
            var defaultSecrecyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.SecrecyLevel);

            // Create a new Document with SourceType = Workflow
            var now = DateTime.Now;
            var storageNumber = $"WF-{now:yyyyMMddHHmmss}";
            createdDocument = await _documentManager.CreateAsync(
                fieldId: null,
                unitId: null,
                workflowId: input.WorkflowId,
                statusId: null,
                typeId: defaultTypeId,
                urgencyLevelId: defaultUrgencyLevelId,
                secrecyLevelId: defaultSecrecyLevelId,
                title: workflowInfo.WorkflowTemplateName,
                completedTime: DateTime.MinValue,
                storageNumber: storageNumber,
                incommingDate: now,
                no: null,
                currentStatus: null,
                sourceType: DocumentSourceType.Workflow
            );

            // Create a DocumentFile for the template file path
            var templateFileName = System.IO.Path.GetFileName(workflowInfo.WordTemplatePath);
            var documentFile = new DocumentFile(
                GuidGenerator.Create(),
                createdDocument.Id,
                templateFileName,
                false,
                now,
                workflowInfo.WordTemplatePath,
                null
            );
            documentFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(documentFile);

            documentId = createdDocument.Id;
            templateDocumentFileId = documentFile.Id;
        }
        else
        {
            // DocumentId is required when not using template file
            if (!input.DocumentId.HasValue || input.DocumentId.Value == default)
                throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);

            documentId = input.DocumentId.Value;
        }

        // Check if document already has an active workflow instance
        var existingInstances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => x.DocumentId == documentId && x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS));
        if (existingInstances.Any())
        {
            throw new UserFriendlyException(L["DocumentAlreadyHasActiveWorkflow"]);
        }

        var firstStep = workflowInfo.Steps.OrderBy(s => s.Order).First();

        // Validate: step 1 must have assigned users
        if (!firstStep.AssignedUsers.Any())
        {
            throw new UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        var nowTime = DateTime.Now;

        // 1. Create DocumentWorkflowInstance
        // StartedAt = now, FinishedAt = now + SLADays (deadline for the first step)
        var firstStepFinishedAt = firstStep.SLADays.HasValue
            ? nowTime.AddDays(firstStep.SLADays.Value)
            : DateTime.MinValue; // No SLA = no deadline

        var instance = await _documentWorkflowInstanceManager.CreateAsync(
            documentId,
            workflowInfo.WorkflowId,
            workflowInfo.WorkflowTemplateId,
            firstStep.StepId,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nowTime,
            firstStepFinishedAt
        );

        // 2. Create DocumentAssignments for step 1 users
        // DocumentFileResultId = the document's file so processors can see/download it
        Guid? signingFileId = templateDocumentFileId;
        if (signingFileId == null)
        {
            // Get the document's first file (ordered by upload date) as the initial signing file
            var documentFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
            signingFileId = documentFiles.OrderBy(f => f.UploadedAt).FirstOrDefault()?.Id ?? input.DocumentFileId;
        }
        foreach (var user in firstStep.AssignedUsers)
        {
            await _documentAssignmentManager.CreateAsync(
                documentId,
                firstStep.StepId,
                user.UserId,
                firstStep.Order,
                firstStep.Type, // PROCESS or SIGN
                nameof(DocumentAssignmentStatus.PENDING),
                nowTime,
                DateTime.MinValue,
                true,
                signingFileId
            );
        }

        // 3. Create DocumentHistory records for each assignment (FromUser = current user, ToUser = receiver)
        foreach (var user in firstStep.AssignedUsers)
        {
            await _documentHistoryManager.CreateAsync(
                documentId,
                CurrentUser.Id,       // FromUser = current user
                user.UserId,          // ToUser = DocumentAssignment ReceiverUserId
                nameof(DocumentHistoryAction.TRINH),     // Action
                input.SigningContent  // Comment = Nội dung trình ký
            );
        }

        // 4. Create log: SubmitWorkflow
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id,
            null, // no assignment yet
            CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW),
            "Initiator",
            null,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            null
        );

        

        // 5. Create DocumentWorkflowInstanceFile records for attached files
        if (input.AttachedFileIds != null && input.AttachedFileIds.Any())
        {
            foreach (var fileId in input.AttachedFileIds)
            {
                var instanceFile = new DocumentWorkflowInstanceFile(
                    GuidGenerator.Create(),
                    instance.Id,
                    fileId
                );
                instanceFile.TenantId = CurrentTenant.Id;
                await _documentWorkflowInstanceFileRepository.InsertAsync(instanceFile);
            }
        }

        // Also attach the template file to the workflow instance if it was created
        if (templateDocumentFileId.HasValue)
        {
            var templateInstanceFile = new DocumentWorkflowInstanceFile(
                GuidGenerator.Create(),
                instance.Id,
                templateDocumentFileId.Value
            );
            templateInstanceFile.TenantId = CurrentTenant.Id;
            await _documentWorkflowInstanceFileRepository.InsertAsync(templateInstanceFile);
        }

        // 6. Send notification to step 1 users
        // Use the in-memory document if just created (not yet committed to DB), otherwise fetch from DB
        var doc = createdDocument ?? await _documentRepository.GetAsync(documentId);
        await SendWorkflowNotificationAsync(
            doc,
            firstStep.AssignedUsers.Select(u => u.UserId).ToList(),
            "WorkflowAssigned",
            $"WorkflowAssignedMessage|{doc.StorageNumber}|{doc.Title}|{workflowInfo.WorkflowName}|{firstStep.Name}"
        );

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(instance);
    }

    /// <summary>
    /// Get the first MasterData ID for the given type. Used as default value when creating workflow-generated documents.
    /// </summary>
    private async Task<Guid> GetDefaultMasterDataIdAsync(MasterDataType type)
    {
        var typeValue = type.GetTypeValue();
        var queryable = await _masterDataRepository.GetQueryableAsync();
        var masterData = await AsyncExecuter.FirstOrDefaultAsync(
            queryable.Where(m => m.Type == typeValue).OrderBy(m => m.CreationTime));

        if (masterData == null)
        {
            throw new UserFriendlyException(L["NoDefaultMasterDataFound"] + $" ({type})");
        }

        return masterData.Id;
    }

    #endregion

    #region ProcessWorkflowActionAsync

    /// <summary>
    /// Process a workflow action: APPROVE, RETURN, REJECT
    /// Includes server-side overdue validation to prevent actions on expired workflows.
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
    {
        // 1. Validate workflow instance
        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.DocumentWorkflowInstanceId);
        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            throw new UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        // 2. Server-side overdue check (prevent actions on expired workflows)
        if (instance.FinishedAt > DateTime.MinValue && instance.FinishedAt <= DateTime.Now)
        {
            throw new UserFriendlyException(L["WorkflowOverdue"]);
        }

        // 3. Validate assignment
        var assignment = await _documentAssignmentRepository.GetAsync(input.DocumentAssignmentId);
        if (assignment.Status != nameof(DocumentAssignmentStatus.PENDING))
        {
            throw new UserFriendlyException(L["AssignmentNotPending"]);
        }

        // 4. Verify current user is the assignment receiver
        if (assignment.ReceiverUserId != CurrentUser.Id!.Value)
        {
            throw new UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        // 5. Verify assignment belongs to this workflow instance's document
        if (assignment.DocumentId != instance.DocumentId)
        {
            throw new UserFriendlyException(L["InvalidWorkflowAction"]);
        }

        var now = DateTime.Now;

        switch (input.Action.ToUpper())
        {
            case nameof(WorkflowInstanceLogAction.APPROVE):
                await HandleApproveAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.RETURN):
                await HandleTerminalActionAsync(instance, assignment, now, input.Note,
                    nameof(DocumentWorkflowInstanceStatus.RETURNED),
                    nameof(WorkflowInstanceLogAction.RETURN),
                    "WorkflowReturned", "WorkflowReturnedMessage",
                    DocumentStatusCode.HT);
                break;
            case nameof(WorkflowInstanceLogAction.REJECT):
                await HandleTerminalActionAsync(instance, assignment, now, input.Note,
                    nameof(DocumentWorkflowInstanceStatus.REJECTED),
                    nameof(WorkflowInstanceLogAction.REJECT),
                    "WorkflowRejected", "WorkflowRejectedMessage",
                    DocumentStatusCode.HT);
                break;
            default:
                throw new UserFriendlyException(L["InvalidWorkflowAction"]);
        }

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(instance);
    }

    private async Task HandleApproveAsync(DocumentWorkflowInstance instance, DocumentAssignment assignment, DateTime now, string? note)
    {
        // 1. Update the current user's assignment
        assignment.Status = nameof(DocumentAssignmentStatus.DONE);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        // IMPORTANT: autoSave=true to flush changes to DB before querying remaining pending assignments.
        // Without this, the subsequent GetListAsync queries the DB which still has the old data
        // (IsCurrent=true, Status=PENDING), causing the workflow to incorrectly think there are
        // still pending assignments and NOT move to the next step.
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Get current step info (needed for all paths)
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        // 3. Check if ALL assignments for the current step are done
        //    (a step may have multiple users - only proceed when everyone approved)
        var remainingPending = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING));

        if (remainingPending.Any())
        {
            // Other users at this step still need to process - just log and return
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

            await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            return;
        }

        // 4. All assignments at current step are done - check if there's a next step
        var allSteps = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        allSteps = allSteps.OrderBy(s => s.Order).ToList();

        var currentIndex = allSteps.FindIndex(s => s.Id == currentStep.Id);
        var isLastStep = currentIndex >= allSteps.Count - 1;

        if (isLastStep)
        {
            // Complete the workflow
            instance.Status = nameof(DocumentWorkflowInstanceStatus.COMPLETED);
            instance.FinishedAt = now;
            await _documentWorkflowInstanceRepository.UpdateAsync(instance);

            // Log
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.COMPLETED), note);

            // Notify workflow initiator
            var document = await _documentRepository.GetAsync(instance.DocumentId);
            await SendWorkflowNotificationAsync(
                document,
                new List<Guid> { instance.CreatorId!.Value },
                "WorkflowCompleted",
                $"WorkflowCompletedMessage|{document.StorageNumber}|{document.Title}"
            );

            // Update document status to HT (Hoàn thành) - last step approved
            await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.HT);
        }
        else
        {
            // Move to next step
            var nextStep = allSteps[currentIndex + 1];
            instance.CurrentStepId = nextStep.Id;

            // Update SLA: StartedAt = now, FinishedAt = now + next step's SLADays
            instance.StartedAt = now;
            instance.FinishedAt = nextStep.SLADays.HasValue
                ? now.AddDays(nextStep.SLADays.Value)
                : DateTime.MinValue; // No SLA = no deadline

            await _documentWorkflowInstanceRepository.UpdateAsync(instance);

            // Log
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

            // Get assignments for next step
            var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
                x => x.StepId == nextStep.Id && x.IsActive);

            // Copy the result file from current step for the next step's assignments
            var nextStepFileId = await CopyDocumentFileForNextStepAsync(
                assignment.DocumentFileResultId, instance.DocumentId);

            var document = await _documentRepository.GetAsync(instance.DocumentId);
            var nextUserIds = new List<Guid>();

            foreach (var sa in stepAssignments.Where(a => a.DefaultUserId.HasValue))
            {
                await _documentAssignmentManager.CreateAsync(
                    instance.DocumentId,
                    nextStep.Id,
                    sa.DefaultUserId!.Value,
                    nextStep.Order,
                    nextStep.Type,
                    nameof(DocumentAssignmentStatus.PENDING),
                    now,
                    DateTime.MinValue,
                    true,
                    nextStepFileId
                );
                nextUserIds.Add(sa.DefaultUserId.Value);
            }

            // Notify next step users
            if (nextUserIds.Any())
            {
                var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);
                await SendWorkflowNotificationAsync(
                    document,
                    nextUserIds,
                    "WorkflowAssigned",
                    $"WorkflowAssignedMessage|{document.StorageNumber}|{document.Title}|{workflow.Name}|{nextStep.Name}"
                );
            }

            // Update document status to DANG_XU_LY (Đang xử lý) - approved but not last step
            await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
        }
    }

    /// <summary>
    /// Shared handler for RETURN and REJECT actions.
    /// - Updates the acting user's assignment
    /// - Revokes all other PENDING assignments at the same step (multi-user step safety)
    /// - Updates workflow instance status
    /// - Creates log, sends notification, updates document status
    /// </summary>
    private async Task HandleTerminalActionAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note,
        string newInstanceStatus,        // e.g. RETURNED or REJECTED
        string logAction,                // e.g. RETURN or REJECT
        string notificationTitleKey,     // e.g. "WorkflowReturned"
        string notificationMessageKey,   // e.g. "WorkflowReturnedMessage"
        DocumentStatusCode documentStatusCode)
    {
        // 1. Update the acting user's assignment
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        // autoSave=true to flush changes to DB before querying other pending assignments
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Revoke all other PENDING assignments at the same step
        //    (when a step has multiple users and one returns/rejects, others must be revoked)
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

        // 3. Update workflow instance status
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        instance.Status = newInstanceStatus;
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        // 4. Log
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            logAction,
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            newInstanceStatus, note);

        // 5. Notify workflow initiator
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            notificationTitleKey,
            $"{notificationMessageKey}|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? "System"}"
        );

        // 6. Update document status
        await UpdateDocumentStatusAsync(instance.DocumentId, documentStatusCode);
    }

    #endregion

    #region GetActiveWorkflowStatusAsync

    /// <summary>
    /// Get active workflow instance status for a document
    /// </summary>
    public async Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId)
    {
        var instances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => x.DocumentId == documentId && x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS));
        var instance = instances.FirstOrDefault();

        if (instance == null)
            return null;

        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        var allSteps = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);

        // Check if current user has an assignment
        DocumentAssignmentInfoDto? myAssignment = null;
        if (CurrentUser.Id.HasValue)
        {
            var myAssignments = await _documentAssignmentRepository.GetListAsync(
                x => x.DocumentId == documentId && x.ReceiverUserId == CurrentUser.Id.Value && x.IsCurrent
            );
            var pending = myAssignments.FirstOrDefault(a => a.Status == nameof(DocumentAssignmentStatus.PENDING));
            if (pending != null)
            {
                var stepForAssignment = await _workflowStepTemplateRepository.FindAsync(pending.WorkflowStepTemplateId ?? Guid.Empty);
                myAssignment = new DocumentAssignmentInfoDto
                {
                    AssignmentId = pending.Id,
                    Status = pending.Status,
                    ActionType = pending.ActionType,
                    StepOrder = pending.StepOrder,
                    StepName = stepForAssignment?.Name ?? "Unknown",
                    IsCurrent = pending.IsCurrent,
                    CanAct = true
                };
            }
        }

        return new DocumentWorkflowStatusDto
        {
            DocumentWorkflowInstanceId = instance.Id,
            DocumentId = documentId,
            Status = instance.Status,
            CurrentStepId = instance.CurrentStepId,
            CurrentStepName = currentStep.Name,
            CurrentStepOrder = currentStep.Order,
            TotalSteps = allSteps.Count,
            StartedAt = instance.StartedAt,
            WorkflowName = workflow.Name,
            MyAssignment = myAssignment
        };
    }

    #endregion

    #region GetDocumentSigningListAsync

    /// <summary>
    /// Get documents for the signing page with filtering
    /// Logic:
    ///   All = Document SourceType=1 AND DocumentAssignment.ReceiverUserId = currentUserId
    ///   SentToMe = DocumentAssignment.ReceiverUserId = currentUserId
    ///   SentByMe = DocumentAssignment.CreatorId = currentUserId
    ///   Following = empty (no logic for now)
    /// </summary>
    public async Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input)
    {
        var currentUserId = CurrentUser.Id!.Value;

        // Get all assignments where current user is receiver
        var receivedAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.ReceiverUserId == currentUserId);
        var receivedDocIds = receivedAssignments.Select(a => a.DocumentId).Distinct().ToList();

        // Get all assignments where current user is creator
        var allAssignmentsQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var createdAssignments = await AsyncExecuter.ToListAsync(
            allAssignmentsQueryable.Where(x => x.CreatorId == currentUserId));
        var createdDocIds = createdAssignments.Select(a => a.DocumentId).Distinct().ToList();

        // All = union of SentToMe + SentByMe + Following
        var allDocIds = receivedDocIds.Union(createdDocIds).Distinct().ToList();

        // Get ALL relevant documents first (to apply date/text filters before counting)
        var allRelevantDocuments = allDocIds.Any()
            ? await _documentRepository.GetListAsync(x => allDocIds.Contains(x.Id))
            : new List<Document>();

        // Apply date filter on IncommingDate
        if (input.FromDate.HasValue)
        {
            allRelevantDocuments = allRelevantDocuments.Where(d => d.IncommingDate >= input.FromDate.Value.Date).ToList();
        }
        if (input.ToDate.HasValue)
        {
            var toDateEnd = input.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
            allRelevantDocuments = allRelevantDocuments.Where(d => d.IncommingDate <= toDateEnd).ToList();
        }

        // Apply text filter
        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            allRelevantDocuments = allRelevantDocuments.Where(d =>
                (d.Title != null && d.Title.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase)) ||
                (d.No != null && d.No.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase)) ||
                (d.StorageNumber != null && d.StorageNumber.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
        }

        // After filtering, calculate counts per category from filtered results
        var filteredDocIdSet = allRelevantDocuments.Select(d => d.Id).ToHashSet();
        int sentToMeCount = receivedDocIds.Count(id => filteredDocIdSet.Contains(id));
        int sentByMeCount = createdDocIds.Count(id => filteredDocIdSet.Contains(id));
        int followingCount = 0; // No logic for now
        int allCount = filteredDocIdSet.Count;

        // Apply filter mode on the already-filtered documents
        List<Document> documents;
        switch (input.FilterMode)
        {
            case DocumentSigningFilterMode.SentToMe:
                documents = allRelevantDocuments.Where(d => receivedDocIds.Contains(d.Id)).ToList();
                break;
            case DocumentSigningFilterMode.SentByMe:
                documents = allRelevantDocuments.Where(d => createdDocIds.Contains(d.Id)).ToList();
                break;
            case DocumentSigningFilterMode.Following:
                documents = new List<Document>(); // Empty for now
                break;
            default: // All
                documents = allRelevantDocuments;
                break;
        }

        var totalCount = documents.Count;

        // Apply paging
        var pagedDocuments = documents
            .OrderByDescending(d => d.IncommingDate)
            .Skip(input.SkipCount)
            .Take(input.MaxResultCount)
            .ToList();

        // Get all workflow instances for the paged documents
        var pagedDocIds = pagedDocuments.Select(d => d.Id).ToList();
        var allInstances = pagedDocIds.Any()
            ? await _documentWorkflowInstanceRepository.GetListAsync(x => pagedDocIds.Contains(x.DocumentId))
            : new List<DocumentWorkflowInstance>();

        // ===== BATCH LOAD: avoid N+1 queries in the loop =====

        // 1. Batch load MasterData for StatusId + TypeId
        var masterDataIds = pagedDocuments
            .SelectMany(d => new[] { d.StatusId, (Guid?)d.TypeId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
        var masterDataDict = masterDataIds.Any()
            ? (await _masterDataRepository.GetListAsync(x => masterDataIds.Contains(x.Id)))
                .ToDictionary(m => m.Id, m => m)
            : new Dictionary<Guid, MasterData>();

        // 2. Batch load Workflows referenced by instances
        var workflowIds = allInstances.Select(i => i.WorkflowId).Distinct().ToList();
        var workflowDict = workflowIds.Any()
            ? (await _workflowRepository.GetListAsync(x => workflowIds.Contains(x.Id)))
                .ToDictionary(w => w.Id, w => w)
            : new Dictionary<Guid, Workflow>();

        // 3. Batch load current steps (WorkflowStepTemplates) referenced by instances
        var stepIds = allInstances.Select(i => i.CurrentStepId).Distinct().ToList();
        var stepDict = stepIds.Any()
            ? (await _workflowStepTemplateRepository.GetListAsync(x => stepIds.Contains(x.Id)))
                .ToDictionary(s => s.Id, s => s)
            : new Dictionary<Guid, WorkflowStepTemplate>();

        // 4. Batch load total step counts per WorkflowTemplate
        var templateIds = allInstances.Select(i => i.WorkflowTemplateId).Distinct().ToList();
        var allStepsForTemplates = templateIds.Any()
            ? await _workflowStepTemplateRepository.GetListAsync(
                x => templateIds.Contains(x.WorkflowTemplateId) && x.IsActive)
            : new List<WorkflowStepTemplate>();
        var totalStepsDict = allStepsForTemplates
            .GroupBy(s => s.WorkflowTemplateId)
            .ToDictionary(g => g.Key, g => g.Count());

        // ===== BUILD ITEMS (no more DB calls in loop) =====
        var items = new List<DocumentSigningItemDto>();
        foreach (var doc in pagedDocuments)
        {
            // Get the latest (or active IN_PROGRESS) instance for this document
            var docInstance = allInstances
                .Where(x => x.DocumentId == doc.Id)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();

            var myDocAssignment = receivedAssignments
                .Where(a => a.DocumentId == doc.Id && a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                .FirstOrDefault();

            // Resolve names from batch-loaded dictionaries
            string? statusName = doc.StatusId.HasValue && masterDataDict.TryGetValue(doc.StatusId.Value, out var statusMd) ? statusMd.Name : null;
            string? typeName = masterDataDict.TryGetValue(doc.TypeId, out var typeMd) ? typeMd.Name : null;

            string? workflowName = null;
            string? currentStepName = null;
            int? currentStepOrder = null;
            int? totalSteps = null;

            if (docInstance != null)
            {
                workflowName = workflowDict.TryGetValue(docInstance.WorkflowId, out var wf) ? wf.Name : null;

                if (stepDict.TryGetValue(docInstance.CurrentStepId, out var step))
                {
                    currentStepName = step.Name;
                    currentStepOrder = step.Order;
                }

                totalStepsDict.TryGetValue(docInstance.WorkflowTemplateId, out var stepsCount);
                totalSteps = stepsCount > 0 ? stepsCount : null;
            }

            items.Add(new DocumentSigningItemDto
            {
                DocumentId = doc.Id,
                DocumentNo = doc.No,
                DocumentTitle = doc.Title,
                StorageNumber = doc.StorageNumber,
                IncommingDate = doc.IncommingDate,
                StatusName = statusName,
                TypeName = typeName,
                WorkflowName = workflowName,
                WorkflowInstanceId = docInstance?.Id,
                WorkflowStatus = docInstance?.Status,
                CurrentStepName = currentStepName,
                CurrentStepOrder = currentStepOrder,
                TotalSteps = totalSteps,
                WorkflowStartedAt = docInstance?.StartedAt,
                MyAssignmentStatus = myDocAssignment?.Status,
                CanAct = myDocAssignment != null && myDocAssignment.Status == nameof(DocumentAssignmentStatus.PENDING),
                MyAssignmentId = myDocAssignment?.Id
            });
        }

        return new DocumentSigningPageResultDto
        {
            TotalCount = totalCount,
            Items = items,
            AllCount = allCount,
            SentToMeCount = sentToMeCount,
            SentByMeCount = sentByMeCount,
            FollowingCount = followingCount
        };
    }

    #endregion

    #region GetWorkflowInstanceLogsAsync / GetWorkflowInstanceFilesAsync / GetDocumentHistoriesByDocumentIdAsync

    /// <summary>
    /// Get workflow instance logs with navigation properties (ActorUser, DocumentAssignment).
    /// Authorized via DocumentAssignments.Default so signing page users can access.
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(Guid workflowInstanceId)
    {
        var logs = await _documentWorkflowInstanceLogsRepository
            .GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(workflowInstanceId);

        return ObjectMapper.Map<List<DocumentWorkflowInstanceLogsWithNavigationProperties>,
            List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>>(logs);
    }

    /// <summary>
    /// Get workflow instance files with navigation properties (DocumentFile).
    /// Authorized via DocumentAssignments.Default so signing page users can access.
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(Guid workflowInstanceId)
    {
        var files = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == workflowInstanceId);

        if (!files.Any())
            return new List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>();

        // Batch load all DocumentFiles at once (avoid N+1)
        var docFileIds = files.Select(f => f.DocumentFileId).Distinct().ToList();
        var docFiles = await _documentFileRepository.GetListAsync(x => docFileIds.Contains(x.Id));
        var docFileDict = docFiles.ToDictionary(f => f.Id, f => f);

        var result = new List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>();
        foreach (var instanceFile in files)
        {
            docFileDict.TryGetValue(instanceFile.DocumentFileId, out var docFile);
            result.Add(new DocumentWorkflowInstanceFileWithNavigationPropertiesDto
            {
                DocumentWorkflowInstanceFile = ObjectMapper.Map<DocumentWorkflowInstanceFile, DocumentWorkflowInstanceFileDto>(instanceFile),
                DocumentFile = docFile != null
                    ? ObjectMapper.Map<DocumentFile, DocumentFileDto>(docFile)
                    : null!
            });
        }

        return result;
    }

    /// <summary>
    /// Get document histories with navigation properties for a document.
    /// Authorized via DocumentAssignments.Default so signing page users can access.
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(Guid documentId)
    {
        var histories = await _documentHistoryRepository.GetHistoryByDocumentIdAsync(
            documentId, skipCount: 0, maxResultCount: 100);

        return ObjectMapper.Map<List<DocumentHistoryWithNavigationProperties>,
            List<DocumentHistoryWithNavigationPropertiesDto>>(histories);
    }

    #endregion

    #region Helpers

    private async Task SendWorkflowNotificationAsync(Document document, List<Guid> receiverUserIds, string titleKey, string contentKey)
    {
        try
        {
            var notification = new Notification(
                GuidGenerator.Create(),
                titleKey,
                contentKey,
                SourceType.DOCUMENT.ToString(),
                EventType.WORKFLOW_ACTION.ToString(),
                RelatedType.DOCUMENT.ToString(),
                "HIGH",
                document.Id.ToString()
            );
            notification.TenantId = CurrentTenant.Id;
            await _notificationRepository.InsertAsync(notification);

            foreach (var userId in receiverUserIds)
            {
                var receiver = new NotificationReceiver(
                    GuidGenerator.Create(),
                    notification.Id,
                    userId,
                    false
                );
                receiver.TenantId = CurrentTenant.Id;
                await _notificationReceiverRepository.InsertAsync(receiver);
            }

            await _distributedEventBus.PublishAsync(
                new NotificationCreatedEto
                {
                    NotificationId = notification.Id,
                    ReceiverUserIds = receiverUserIds
                }
            );
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending workflow notification for document {DocumentId}", document.Id);
            // Don't throw - notification failure shouldn't block workflow action
        }
    }

    #endregion

    #region CheckAndHandleOverdueAsync

    /// <summary>
    /// Check if a workflow instance is overdue and handle it.
    /// Returns overdue status and whether the current step allows return action.
    /// If overdue: updates Document status to DA_HUY, creates DocumentHistory,
    /// sets instance status to CANCELLED, creates a log entry.
    /// Thread-safe: checks status before updating to prevent duplicate overdue handling.
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId)
    {
        var result = new WorkflowOverdueCheckResultDto
        {
            IsOverdue = false,
            AllowReturn = false
        };

        try
        {
            var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            result.AllowReturn = currentStep.AllowReturn;

            // Check overdue: FinishedAt must be set (> MinValue), FinishedAt <= now,
            // and status must not be terminal (COMPLETED, REJECTED, CANCELLED, RETURNED)
            var terminalStatuses = new[]
            {
                nameof(DocumentWorkflowInstanceStatus.COMPLETED),
                nameof(DocumentWorkflowInstanceStatus.REJECTED),
                nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                nameof(DocumentWorkflowInstanceStatus.RETURNED)
            };

            if (instance.FinishedAt > DateTime.MinValue
                && instance.FinishedAt <= DateTime.Now
                && !terminalStatuses.Contains(instance.Status))
            {
                result.IsOverdue = true;

                // Perform overdue updates using the common helper (pass instance to avoid re-fetch)
                await UpdateWorkflowStatusCommonAsync(
                    instance: instance,
                    documentStatusCode: DocumentStatusCode.DA_HUY,
                    historyComment: "Hết hạn xử lý tài liệu",
                    workflowInstanceStatus: nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                    logNote: "Hết hạn xử lý tài liệu",
                    logAction: nameof(WorkflowInstanceLogAction.WORKFLOW_COMPLETED)
                );
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking/handling overdue for workflow instance {InstanceId}", workflowInstanceId);
            // Return safe defaults - don't block the UI
        }

        return result;
    }

    #endregion

    #region UpdateWorkflowStatusCommonAsync

    /// <summary>
    /// Common helper to update workflow-related entities in batch.
    /// Each parameter is optional: null/empty means skip that update.
    /// Each step is wrapped in try-catch so one failure doesn't block the rest.
    /// Accepts a pre-loaded instance to avoid unnecessary re-fetch from DB.
    /// </summary>
    private async Task UpdateWorkflowStatusCommonAsync(
        DocumentWorkflowInstance instance,
        DocumentStatusCode? documentStatusCode,
        string historyComment,
        string? workflowInstanceStatus,
        string logNote,
        string? logAction = null)
    {
        var previousStatus = instance.Status;
        var effectiveLogAction = logAction ?? nameof(WorkflowInstanceLogAction.WORKFLOW_COMPLETED);

        // 1. Update Document status (null = skip)
        if (documentStatusCode.HasValue)
        {
            try
            {
                await UpdateDocumentStatusAsync(instance.DocumentId, documentStatusCode.Value);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "UpdateWorkflowStatusCommon: Failed to update document status for instance {InstanceId}", instance.Id);
            }
        }

        // 2. Create DocumentHistory (empty = skip)
        if (!string.IsNullOrEmpty(historyComment))
        {
            try
            {
                await _documentHistoryManager.CreateAsync(
                    instance.DocumentId,
                    CurrentUser.Id,              // FromUser
                    CurrentUser.Id ?? Guid.Empty, // ToUser (self for system actions)
                    effectiveLogAction,           // Action
                    historyComment                // Comment
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "UpdateWorkflowStatusCommon: Failed to create document history for instance {InstanceId}", instance.Id);
            }
        }

        // 3. Update DocumentWorkflowInstances status (null = skip)
        if (!string.IsNullOrEmpty(workflowInstanceStatus))
        {
            try
            {
                instance.Status = workflowInstanceStatus;
                instance.FinishedAt = DateTime.Now;
                await _documentWorkflowInstanceRepository.UpdateAsync(instance);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "UpdateWorkflowStatusCommon: Failed to update instance status for instance {InstanceId}", instance.Id);
            }
        }

        // 4. Create DocumentWorkflowInstanceLogs (empty = skip)
        if (!string.IsNullOrEmpty(logNote))
        {
            try
            {
                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id,
                    null,                          // no specific assignment
                    CurrentUser.Id,
                    effectiveLogAction,
                    "System",
                    previousStatus,
                    workflowInstanceStatus ?? instance.Status,
                    logNote
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "UpdateWorkflowStatusCommon: Failed to create log for instance {InstanceId}", instance.Id);
            }
        }
    }

    #endregion

    #region CopyDocumentFileForNextStepAsync

    /// <summary>
    /// Copy a DocumentFile (and its blob) for the next workflow step.
    /// Each step gets its own copy of the file so the audit trail is clear.
    /// - Reads the source blob from storage
    /// - Creates a new blob at a new path (signing-steps/{guid}{extension})
    /// - Creates a new DocumentFile record pointing to the new blob
    /// Returns the new DocumentFile ID, or null if no source file found.
    /// </summary>
    private async Task<Guid?> CopyDocumentFileForNextStepAsync(Guid? sourceFileId, Guid documentId)
    {
        if (!sourceFileId.HasValue)
        {
            // Fallback: try to get any completed assignment's result file for this document
            var completedAssignments = await _documentAssignmentRepository.GetListAsync(
                x => x.DocumentId == documentId
                && x.Status == nameof(DocumentAssignmentStatus.DONE)
                && x.DocumentFileResultId.HasValue);
            sourceFileId = completedAssignments
                .OrderByDescending(a => a.ProcessedAt)
                .FirstOrDefault()?.DocumentFileResultId;
        }

        if (!sourceFileId.HasValue) return null;

        var sourceFile = await _documentFileRepository.FindAsync(sourceFileId.Value);
        if (sourceFile == null || string.IsNullOrEmpty(sourceFile.Path)) return null;

        try
        {
            // Read the source blob
            var fileBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);

            // Create new blob path
            var extension = Path.GetExtension(sourceFile.Name);
            var newBlobPath = $"signing-steps/{Guid.NewGuid()}{extension}";

            // Upload to new path in blob storage
            await _blobContainer.SaveAsync(newBlobPath, fileBytes);

            // Create new DocumentFile record
            var newFile = new DocumentFile(
                GuidGenerator.Create(),
                documentId,
                sourceFile.Name,
                sourceFile.IsSigned,
                DateTime.Now,
                newBlobPath,
                sourceFile.Hash
            );
            newFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(newFile);

            return newFile.Id;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error copying document file for next step. SourceFileId={SourceFileId}, DocumentId={DocumentId}",
                sourceFileId, documentId);
            // Fallback: return the source file ID directly (no copy, but at least the reference is there)
            return sourceFileId;
        }
    }

    #endregion

    #region UpdateDocumentStatusAsync

    /// <summary>
    /// Update document status by DocumentStatusCode enum.
    /// Looks up MasterData by Code and Type = "TRANG_THAI_VB".
    /// </summary>
    private async Task UpdateDocumentStatusAsync(Guid documentId, DocumentStatusCode statusCode)
    {
        try
        {
            var document = await _documentRepository.GetAsync(documentId);
            var code = statusCode.GetCode();

            var statusList = await _masterDataRepository.GetListAsync(
                x => x.Code == code && x.Type == MasterDataType.Status.GetTypeValue());

            var status = statusList.FirstOrDefault();
            if (status == null)
            {
                Logger.LogWarning("MasterData with Code='{Code}' and Type='TRANG_THAI_VB' not found. Document status will not be updated.", code);
                return;
            }

            document.StatusId = status.Id;
            await _documentRepository.UpdateAsync(document);

            Logger.LogInformation("Document status updated to {Code}: DocumentId={DocumentId}, StatusId={StatusId}",
                code, documentId, status.Id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating document status to {Code}: DocumentId={DocumentId}",
                statusCode.GetCode(), documentId);
            // Don't throw - we don't want to fail the workflow action if status update fails
        }
    }

    #endregion
}
