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
        IDocumentHistoryRepository documentHistoryRepository
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
        var instance = await _documentWorkflowInstanceManager.CreateAsync(
            documentId,
            workflowInfo.WorkflowId,
            workflowInfo.WorkflowTemplateId,
            firstStep.StepId,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nowTime,
            DateTime.MinValue
        );

        // 2. Create DocumentAssignments for step 1 users
        var signingFileId = templateDocumentFileId ?? input.DocumentFileId;
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
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
    {
        // Validate
        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.DocumentWorkflowInstanceId);
        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            throw new UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        var assignment = await _documentAssignmentRepository.GetAsync(input.DocumentAssignmentId);
        if (assignment.Status != nameof(DocumentAssignmentStatus.PENDING))
        {
            throw new UserFriendlyException(L["AssignmentNotPending"]);
        }

        // Verify current user is the assignment receiver
        if (assignment.ReceiverUserId != CurrentUser.Id!.Value)
        {
            throw new UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        var now = DateTime.Now;
        var previousStatus = instance.Status;

        switch (input.Action.ToUpper())
        {
            case nameof(WorkflowInstanceLogAction.APPROVE):
                await HandleApproveAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.RETURN):
                await HandleReturnAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.REJECT):
                await HandleRejectAsync(instance, assignment, now, input.Note);
                break;
            default:
                throw new UserFriendlyException(L["InvalidWorkflowAction"]);
        }

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(instance);
    }

    private async Task HandleApproveAsync(DocumentWorkflowInstance instance, DocumentAssignment assignment, DateTime now, string? note)
    {
        // 1. Update assignment
        assignment.Status = nameof(DocumentAssignmentStatus.DONE);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment);

        // 2. Check if there's a next step
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
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
        }
        else
        {
            // Move to next step
            var nextStep = allSteps[currentIndex + 1];
            instance.CurrentStepId = nextStep.Id;
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
                    null
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
        }
    }

    private async Task HandleReturnAsync(DocumentWorkflowInstance instance, DocumentAssignment assignment, DateTime now, string? note)
    {
        // 1. Update assignment
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment);

        // 2. Update instance
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        instance.Status = nameof(DocumentWorkflowInstanceStatus.RETURNED);
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        // 3. Log
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.RETURN),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.RETURNED), note);

        // 4. Notify workflow initiator
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowReturned",
            $"WorkflowReturnedMessage|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? "System"}"
        );
    }

    private async Task HandleRejectAsync(DocumentWorkflowInstance instance, DocumentAssignment assignment, DateTime now, string? note)
    {
        // 1. Update assignment
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment);

        // 2. Update instance
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        instance.Status = nameof(DocumentWorkflowInstanceStatus.REJECTED);
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        // 3. Log
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.REJECT),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.REJECTED), note);

        // 4. Notify workflow initiator
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowRejected",
            $"WorkflowRejectedMessage|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? "System"}"
        );
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

        // Count for each filter mode
        int sentToMeCount = receivedDocIds.Count;
        int sentByMeCount = createdDocIds.Count;
        int followingCount = 0; // No logic for now
        int allCount = allDocIds.Count;

        // Apply filter mode
        List<Guid> filteredDocIds;
        switch (input.FilterMode)
        {
            case DocumentSigningFilterMode.SentToMe:
                filteredDocIds = receivedDocIds;
                break;
            case DocumentSigningFilterMode.SentByMe:
                filteredDocIds = createdDocIds;
                break;
            case DocumentSigningFilterMode.Following:
                filteredDocIds = new List<Guid>(); // Empty for now
                break;
            default: // All = union of SentToMe + SentByMe + Following
                filteredDocIds = allDocIds;
                break;
        }

        // Get documents
        var documents = filteredDocIds.Any()
            ? await _documentRepository.GetListAsync(x => filteredDocIds.Contains(x.Id))
            : new List<Document>();

        // Apply date filter on IncommingDate
        if (input.FromDate.HasValue)
        {
            documents = documents.Where(d => d.IncommingDate >= input.FromDate.Value.Date).ToList();
        }
        if (input.ToDate.HasValue)
        {
            var toDateEnd = input.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
            documents = documents.Where(d => d.IncommingDate <= toDateEnd).ToList();
        }

        // Apply text filter
        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            documents = documents.Where(d =>
                (d.Title != null && d.Title.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase)) ||
                (d.No != null && d.No.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase)) ||
                (d.StorageNumber != null && d.StorageNumber.Contains(input.FilterText, StringComparison.OrdinalIgnoreCase))
            ).ToList();
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

        // Build items
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

            // Get status and type names
            string? statusName = null;
            string? typeName = null;
            string? workflowName = null;
            string? currentStepName = null;
            int? currentStepOrder = null;
            int? totalSteps = null;

            if (doc.StatusId.HasValue)
            {
                var status = await _masterDataRepository.FindAsync(doc.StatusId.Value);
                statusName = status?.Name;
            }

            var type = await _masterDataRepository.FindAsync(doc.TypeId);
            typeName = type?.Name;

            if (docInstance != null)
            {
                var workflow = await _workflowRepository.FindAsync(docInstance.WorkflowId);
                workflowName = workflow?.Name;

                var step = await _workflowStepTemplateRepository.FindAsync(docInstance.CurrentStepId);
                currentStepName = step?.Name;
                currentStepOrder = step?.Order;

                var steps = await _workflowStepTemplateRepository.GetListAsync(
                    x => x.WorkflowTemplateId == docInstance.WorkflowTemplateId && x.IsActive);
                totalSteps = steps.Count;
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

        // Load navigation properties (DocumentFile) manually
        var result = new List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>();
        foreach (var instanceFile in files)
        {
            var docFile = await _documentFileRepository.FindAsync(instanceFile.DocumentFileId);
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
}
