using HC.Shared;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.Documents;
using HC.DocumentAssignments;
using HC.DocumentWorkflowInstanceLogss;
using HC.Notifications;
using HC.NotificationReceivers;
using HC.UserSignatures;
using HC.SignatureSettings;
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
using Volo.Abp.Uow;
using UglyToad.PdfPig;
using PdfSharpPdf = PdfSharp.Pdf;
using PdfSharpIO = PdfSharp.Pdf.IO;
using PdfSharpDrawing = PdfSharp.Drawing;

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
    private readonly IUserSignatureRepository _userSignatureRepository;

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
        IBlobContainer blobContainer,
        IUserSignatureRepository userSignatureRepository
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
        _userSignatureRepository = userSignatureRepository;
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

        // ISSUE-07 FIX: Sort by CreationTime descending to consistently get the latest template
        // (FullAuditedAggregateRoot handles soft-delete via IsDeleted filter automatically)
        var templates = await _workflowTemplateRepository.GetListAsync(x => x.WorkflowId == workflowId);
        var activeTemplate = templates.OrderByDescending(x => x.CreationTime).FirstOrDefault();
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
            PdfTemplatePath = activeTemplate.PdfTemplatePath,
            HasTemplateFile = !string.IsNullOrWhiteSpace(activeTemplate.PdfTemplatePath),
            SignMode = activeTemplate.SignMode,
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
    /// Submit a document to a workflow.
    /// ISSUE-13 FIX: Explicit [UnitOfWork] to ensure all DB operations (document, file, instance,
    /// assignments, history, logs, notifications) are committed atomically. If any step fails,
    /// all previous operations are rolled back.
    /// </summary>
    [UnitOfWork]
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
            //Workflow tạm 
            //Chính là nếu file doc docx thì convert sang pdf trước khi tạo document
            
            if (!workflowInfo.HasTemplateFile || string.IsNullOrWhiteSpace(workflowInfo.PdfTemplatePath))
            {
                throw new UserFriendlyException(L["WorkflowTemplateHasNoFile"]);
            }

            // Get default MasterData values for required fields
            var defaultTypeId = await GetDefaultMasterDataIdAsync(MasterDataType.DocumentType);
            var defaultUrgencyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.UrgencyLevel);
            var defaultSecrecyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.SecrecyLevel);

            // Create a new Document with SourceType = Workflow
            var now = Clock.Now; // ISSUE-08 FIX: Use ABP Clock instead of DateTime.Now
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
            // IMPORTANT: autoSave=true so the file record is flushed to DB before
            // CopyDocumentFileForNextStepAsync queries it for PARALLEL mode step 2+ copies.
            var templateFileName = System.IO.Path.GetFileName(workflowInfo.PdfTemplatePath);
            var documentFile = new DocumentFile(
                GuidGenerator.Create(),
                createdDocument.Id,
                templateFileName,
                false,
                now,
                workflowInfo.PdfTemplatePath,
                null
            );
            documentFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(documentFile, autoSave: true);

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

        // ISSUE-09 FIX: Cleanup old workflow-related assignments from previous RETURNED/REJECTED instances.
        // When re-submitting, mark old assignments as obsolete so they don't appear in queries.
        var oldTerminatedInstances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => x.DocumentId == documentId &&
            (x.Status == nameof(DocumentWorkflowInstanceStatus.RETURNED) ||
             x.Status == nameof(DocumentWorkflowInstanceStatus.REJECTED)));

        if (oldTerminatedInstances.Any())
        {
            var oldAssignments = await _documentAssignmentRepository.GetListAsync(
                x => x.DocumentId == documentId
                && x.IsCurrent
                && (x.Status == nameof(DocumentAssignmentStatus.REJECTED)
                    || x.Status == nameof(DocumentAssignmentStatus.REVOKE)));

            foreach (var oldAssignment in oldAssignments)
            {
                oldAssignment.IsCurrent = false; // Mark as not current so they don't interfere with new workflow
                await _documentAssignmentRepository.UpdateAsync(oldAssignment);
            }

            if (oldAssignments.Any())
            {
                Logger.LogInformation(
                    "[RE_SUBMIT] Cleaned up {Count} old assignments for document {DocumentId} before re-submit.",
                    oldAssignments.Count, documentId);
            }
        }

        var allStepsOrdered = workflowInfo.Steps.OrderBy(s => s.Order).ToList();
        var firstStep = allStepsOrdered.First();
        var isParallel = workflowInfo.SignMode == nameof(SignMode.PARALLEL);

        // Validate: step 1 must have assigned users (for both SEQUENTIAL and PARALLEL)
        if (!firstStep.AssignedUsers.Any())
        {
            throw new UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        // For PARALLEL: validate ALL steps have assigned users
        if (isParallel)
        {
            foreach (var step in allStepsOrdered)
            {
                if (!step.AssignedUsers.Any())
                {
                    throw new UserFriendlyException(L["AllStepsMustHaveAssignedUsers"]);
                }
            }
        }

        var nowTime = Clock.Now; // ISSUE-08 FIX

        // 1. Create DocumentWorkflowInstance
        // SEQUENTIAL: FinishedAt = now + step1 SLADays
        // PARALLEL: FinishedAt = now + max SLADays across all steps (all run concurrently)
        DateTime finishedAt;
        if (isParallel)
        {
            var maxSlaDays = allStepsOrdered
                .Where(s => s.SLADays.HasValue)
                .Select(s => s.SLADays!.Value)
                .DefaultIfEmpty(0)
                .Max();
            finishedAt = maxSlaDays > 0 ? nowTime.AddDays(maxSlaDays) : DateTime.MinValue;
        }
        else
        {
            finishedAt = firstStep.SLADays.HasValue
                ? nowTime.AddDays(firstStep.SLADays.Value)
                : DateTime.MinValue;
        }

        var instance = await _documentWorkflowInstanceManager.CreateAsync(
            documentId,
            workflowInfo.WorkflowId,
            workflowInfo.WorkflowTemplateId,
            firstStep.StepId,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nowTime,
            finishedAt
        );

        // 2. Resolve the initial signing file (used as source for all assignments)
        Guid? signingFileId = templateDocumentFileId;
        if (signingFileId == null)
        {
            var documentFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
            signingFileId = documentFiles.OrderBy(f => f.UploadedAt).FirstOrDefault()?.Id ?? input.DocumentFileId;
        }

        // 3. Create DocumentAssignments
        // SEQUENTIAL: only step 1 assignments (IsCurrent = true)
        // PARALLEL: ALL steps assignments at once (all IsCurrent = true), each gets its own file copy
        var stepsToAssign = isParallel ? allStepsOrdered : new List<WorkflowStepDetailDto> { firstStep };
        var allNotifyUserIds = new List<Guid>();

        foreach (var step in stepsToAssign)
        {
            // For PARALLEL: each step/user gets its own copy of the file
            // For SEQUENTIAL step 1: all users share the same source file
            Guid? stepFileId = signingFileId;
            if (isParallel && step.Order > firstStep.Order)
            {
                // Copy the file for each subsequent step in parallel mode
                stepFileId = await CopyDocumentFileForNextStepAsync(signingFileId, documentId);
                if (!stepFileId.HasValue)
                {
                    Logger.LogError("[SUBMIT] CopyDocumentFileForNextStepAsync returned null for parallel step {StepOrder}. SigningFileId={SigningFileId}, DocumentId={DocumentId}",
                        step.Order, signingFileId, documentId);
                    throw new UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
                }
            }

            foreach (var user in step.AssignedUsers)
            {
                await _documentAssignmentManager.CreateAsync(
                    documentId,
                    step.StepId,
                    user.UserId,
                    step.Order,
                    step.Type, // PROCESS or SIGN
                    nameof(DocumentAssignmentStatus.PENDING),
                    nowTime,
                    DateTime.MinValue,
                    true, // IsCurrent = true for all PARALLEL assignments
                    stepFileId
                );

                allNotifyUserIds.Add(user.UserId);
            }
        }

        // 4. Create DocumentHistory records (FromUser = current user, ToUser = each receiver)
        foreach (var step in stepsToAssign)
        {
            foreach (var user in step.AssignedUsers)
            {
                await _documentHistoryManager.CreateAsync(
                    documentId,
                    CurrentUser.Id,
                    user.UserId,
                    nameof(DocumentHistoryAction.TRINH),
                    input.SigningContent
                );
            }
        }

        // 5. Create log: SubmitWorkflow
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id,
            null,
            CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW),
            WorkflowConstants.RoleInitiator,
            null,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            isParallel ? $"PARALLEL - {allStepsOrdered.Count} steps" : null
        );

        // 6. Create DocumentWorkflowInstanceFile records for attached files
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

        // 7. Send notification to assigned users
        var doc = createdDocument ?? await _documentRepository.GetAsync(documentId);
        var distinctNotifyUserIds = allNotifyUserIds.Distinct().ToList();
        if (distinctNotifyUserIds.Any())
        {
            await SendWorkflowNotificationAsync(
                doc,
                distinctNotifyUserIds,
                "WorkflowAssigned",
                $"WorkflowAssignedMessage|{doc.StorageNumber}|{doc.Title}|{workflowInfo.WorkflowName}|{firstStep.Name}"
            );
        }

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
            queryable.Where(m => m.Type == typeValue).OrderBy(m => m.SortOrder));

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
        if (instance.FinishedAt > DateTime.MinValue && instance.FinishedAt <= Clock.Now)
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

        var now = Clock.Now; // ISSUE-08 FIX

        if (input.Action.ToUpper() == nameof(WorkflowInstanceLogAction.APPROVE) && input.SigningMethodId.HasValue)
        {
            var signingMethod = await _masterDataRepository.FindAsync(input.SigningMethodId.Value);
            Logger.LogInformation("SigningMethod, SigningMethodCode: {SigningMethodId}, SigningMethod: {SigningMethod}", input.SigningMethodId, signingMethod?.Code);
            if (signingMethod != null && signingMethod.Code == nameof(SignType.ELECTRONIC))
            {
                Logger.LogInformation("Apply Electronic Signature");

                await ApplyElectronicSignatureAsync(assignment, instance, input.Note);
            }
            // Note: DIGITAL signing (signingMethod.Code == nameof(SignType.DIGITAL)) is not yet implemented
        }

        switch (input.Action.ToUpper())
        {
            case nameof(WorkflowInstanceLogAction.APPROVE):
                await HandleApproveAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.RETURN):
                await HandleReturnAsync(instance, assignment, now, input.Note);
                break;
            case nameof(WorkflowInstanceLogAction.REJECT):
                await HandleTerminalActionAsync(instance, assignment, now, input.Note,
                    nameof(DocumentWorkflowInstanceStatus.REJECTED),
                    nameof(WorkflowInstanceLogAction.REJECT),
                    "WorkflowRejected", "WorkflowRejectedMessage",
                    DocumentStatusCode.TU_CHOI);  // ISSUE-03 FIX: REJECT → TU_CHOI instead of HT
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
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Determine sign mode (SEQUENTIAL or PARALLEL) from the workflow template
        var template = await _workflowTemplateRepository.GetAsync(instance.WorkflowTemplateId);
        var isParallel = template.SignMode == nameof(SignMode.PARALLEL);

        // 3. Get current step info (needed for logging)
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        // 4. ISSUE-FIX: When ANY user at a step signs (primary or secondary), treat the step as complete.
        //    Revoke ALL other PENDING assignments at the SAME step so the step is considered done.
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
            Logger.LogInformation("[APPROVE] Revoked same-step assignment {AssignmentId} for user {UserId} (secondary user auto-revoke)",
                other.Id, other.ReceiverUserId);
        }

        // 5. Check remaining PENDING assignments across OTHER steps
        //    For SEQUENTIAL: should be none since only current step has IsCurrent=true
        //    For PARALLEL: checks ALL remaining steps (all were created as IsCurrent=true at submit)
        var remainingPending = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING));

        if (remainingPending.Any())
        {
            // Other STEPS still need to process - just log and return
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

            await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            return;
        }

        // ===== RACE CONDITION GUARD (ISSUE-01 FIX) =====
        // Re-fetch instance from DB to check if another concurrent thread already completed it.
        // This prevents duplicate completion in PARALLEL mode when 2 users approve simultaneously.
        var freshInstance = await _documentWorkflowInstanceRepository.GetAsync(instance.Id);
        if (freshInstance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            Logger.LogWarning(
                "[RACE_GUARD] Workflow {InstanceId} already transitioned to {Status} by another thread. " +
                "Current user {UserId} approve logged but completion skipped.",
                instance.Id, freshInstance.Status, CurrentUser.Id);

            // Still log this user's approve action (the assignment was already marked DONE above)
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                freshInstance.Status,
                freshInstance.Status,
                $"[RACE_GUARD] Workflow already {freshInstance.Status}. {note}");
            return;
        }
        // Use the fresh instance for all subsequent operations to avoid stale data
        instance = freshInstance;

        // 5. All assignments are done
        if (isParallel)
        {
            // ===== PARALLEL: All steps completed → merge signed PDFs + complete workflow =====
            await HandleParallelCompleteAsync(instance, assignment, currentStep, now, note);
        }
        else
        {
            // ===== SEQUENTIAL: Check if there's a next step =====
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

                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id, assignment.Id, CurrentUser.Id,
                    nameof(WorkflowInstanceLogAction.APPROVE),
                    currentStep.Type,
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    nameof(DocumentWorkflowInstanceStatus.COMPLETED), note);

                var document = await _documentRepository.GetAsync(instance.DocumentId);
                await SendWorkflowNotificationAsync(
                    document,
                    new List<Guid> { instance.CreatorId!.Value },
                    "WorkflowCompleted",
                    $"WorkflowCompletedMessage|{document.StorageNumber}|{document.Title}"
                );

                await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.HT);
            }
            else
            {
                // Move to next step
                var nextStep = allSteps[currentIndex + 1];
                instance.CurrentStepId = nextStep.Id;
                // ISSUE-10 FIX: Do NOT overwrite StartedAt - it tracks when the workflow was created.
                // instance.StartedAt = now; // REMOVED - preserves original workflow start time
                instance.FinishedAt = nextStep.SLADays.HasValue
                    ? now.AddDays(nextStep.SLADays.Value)
                    : DateTime.MinValue;

                await _documentWorkflowInstanceRepository.UpdateAsync(instance);

                await _documentWorkflowInstanceLogsManager.CreateAsync(
                    instance.Id, assignment.Id, CurrentUser.Id,
                    nameof(WorkflowInstanceLogAction.APPROVE),
                    currentStep.Type,
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                    nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

                var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
                    x => x.StepId == nextStep.Id && x.IsActive);

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

                await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            }
        }
    }

    /// <summary>
    /// Handle workflow completion for PARALLEL mode.
    /// All assignments across all steps are DONE → merge signed PDFs and complete workflow.
    /// 
    /// Merge strategy: take the original PDF template, then apply all signatures sequentially
    /// (each step has unique placeholders like Sign01, Sign02, so they don't conflict).
    /// </summary>
    private async Task HandleParallelCompleteAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment triggeringAssignment,
        WorkflowStepTemplate currentStep,
        DateTime now,
        string? note)
    {
        Logger.LogInformation("[PARALLEL_COMPLETE] All parallel assignments done. Starting merge for instance {InstanceId}", instance.Id);

        // 1. Merge all signed PDFs into one final document
        try
        {
            await MergeSignedPdfsForParallelAsync(instance);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[PARALLEL_COMPLETE] Error merging signed PDFs for instance {InstanceId}. Completing workflow without merge.", instance.Id);
            // Don't block workflow completion if merge fails
        }

        // 2. Complete the workflow
        instance.Status = nameof(DocumentWorkflowInstanceStatus.COMPLETED);
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        // 3. Log
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, triggeringAssignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.APPROVE),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.COMPLETED),
            $"PARALLEL workflow completed - all steps done. {note}");

        // 4. Notify workflow initiator
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowCompleted",
            $"WorkflowCompletedMessage|{document.StorageNumber}|{document.Title}"
        );

        // 5. Update document status to HT (Hoàn thành)
        await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.HT);

        Logger.LogInformation("[PARALLEL_COMPLETE] Parallel workflow completed successfully. InstanceId={InstanceId}", instance.Id);
    }

    /// <summary>
    /// BUG-3 FIX: Now used ONLY for REJECT action (RETURN has its own HandleReturnAsync).
    /// - Updates the acting user's assignment
    /// - Revokes ALL other PENDING assignments (entire workflow is terminated on REJECT)
    /// - Updates workflow instance status
    /// - Creates log, sends notification, updates document status
    /// </summary>
    private async Task HandleTerminalActionAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note,
        string newInstanceStatus,        // e.g. REJECTED
        string logAction,                // e.g. REJECT
        string notificationTitleKey,     // e.g. "WorkflowRejected"
        string notificationMessageKey,   // e.g. "WorkflowRejectedMessage"
        DocumentStatusCode documentStatusCode)
    {
        // 1. Update the acting user's assignment
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        // autoSave=true to flush changes to DB before querying other pending assignments
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Revoke ALL other PENDING assignments (REJECT terminates entire workflow)
        // ISSUE-14 FIX: For SEQUENTIAL mode, filter by StepOrder as a safety guard
        // to avoid revoking stale assignments from previous steps if data inconsistency occurs.
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
            $"{notificationMessageKey}|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? WorkflowConstants.RoleSystem}"
        );

        // 6. Update document status
        await UpdateDocumentStatusAsync(instance.DocumentId, documentStatusCode);
    }

    #endregion

    #region HandleReturnAsync

    /// <summary>
    /// Handle RETURN action: Reset workflow back to step 1 instead of terminating.
    /// - PARALLEL: Cancel ALL pending assignments across all steps, then reset to step 1
    /// - SEQUENTIAL: Cancel pending assignments at current step, then reset to step 1
    /// The workflow instance status is set to RETURNED and document status to TRA_VE.
    /// The initiator can then re-submit (edit and re-send) the workflow from the signing page.
    /// </summary>
    private async Task HandleReturnAsync(
        DocumentWorkflowInstance instance,
        DocumentAssignment assignment,
        DateTime now,
        string? note)
    {
        // 1. Update the acting user's assignment
        assignment.Status = nameof(DocumentAssignmentStatus.REJECTED);
        assignment.ProcessedAt = now;
        assignment.IsCurrent = false;
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Determine sign mode (SEQUENTIAL or PARALLEL)
        var template = await _workflowTemplateRepository.GetAsync(instance.WorkflowTemplateId);
        var isParallel = template.SignMode == nameof(SignMode.PARALLEL);

        // 3. Cancel/Revoke ALL other pending assignments
        // For PARALLEL: cancel ALL pending assignments across ALL steps (entire workflow resets)
        // For SEQUENTIAL: cancel all pending at current step
        var otherPendingAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING)
            && x.Id != assignment.Id);

        if (!isParallel)
        {
            // SEQUENTIAL: only revoke same-step assignments (safety guard)
            otherPendingAssignments = otherPendingAssignments
                .Where(x => x.StepOrder == assignment.StepOrder).ToList();
        }
        // PARALLEL: revoke ALL pending assignments (all steps)

        foreach (var other in otherPendingAssignments)
        {
            other.Status = nameof(DocumentAssignmentStatus.REVOKE);
            other.ProcessedAt = now;
            other.IsCurrent = false;
            await _documentAssignmentRepository.UpdateAsync(other);
        }

        // 4. Update workflow instance status to RETURNED
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        // Reset CurrentStepId to the first step so re-submit starts from step 1
        var allSteps = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        var firstStep = allSteps.OrderBy(s => s.Order).First();

        instance.Status = nameof(DocumentWorkflowInstanceStatus.RETURNED);
        instance.CurrentStepId = firstStep.Id; // Reset to first step
        instance.FinishedAt = now;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        // 5. Log the RETURN action
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id, assignment.Id, CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.RETURN),
            currentStep.Type,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.RETURNED), note);

        // 6. Notify workflow initiator
        var document = await _documentRepository.GetAsync(instance.DocumentId);
        await SendWorkflowNotificationAsync(
            document,
            new List<Guid> { instance.CreatorId!.Value },
            "WorkflowReturned",
            $"WorkflowReturnedMessage|{document.StorageNumber}|{document.Title}|{CurrentUser.UserName ?? WorkflowConstants.RoleSystem}"
        );

        // 7. Update document status to TRA_VE
        await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.TRA_VE);
    }

    #endregion

    #region ResubmitReturnedWorkflowAsync

    /// <summary>
    /// Re-submit a workflow that was previously returned (RETURNED status).
    /// REUSES the same workflow instance (preserves all workflow logs history).
    /// Allows the initiator to edit signing content, re-attach files, change document/file selection.
    /// Resets the instance back to IN_PROGRESS at step 1.
    /// </summary>
    [UnitOfWork]
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input)
    {
        // 1. Validate the returned workflow instance
        var returnedInstance = await _documentWorkflowInstanceRepository.GetAsync(input.ReturnedWorkflowInstanceId);
        if (returnedInstance.Status != nameof(DocumentWorkflowInstanceStatus.RETURNED))
        {
            throw new UserFriendlyException(L["WorkflowNotReturned"]);
        }

        // 2. Verify current user is the workflow initiator
        if (returnedInstance.CreatorId != CurrentUser.Id!.Value)
        {
            throw new UserFriendlyException(L["OnlyInitiatorCanResubmit"]);
        }

        var documentId = returnedInstance.DocumentId;

        // 3. If user wants to change the document file, handle it
        // ISSUE-1 FIX: When using template file, REUSE the existing DocumentFile instead of creating a duplicate.
        // The original submission already created a DocumentFile from the template path.
        Guid? newSigningFileId = null;

        if (input.UseWorkflowTemplateFile)
        {
            // Find the existing template-based DocumentFile for this document (reuse, don't duplicate)
            var existingDocFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
            var workflowInfo = await GetWorkflowSubmitInfoAsync(returnedInstance.WorkflowId);

            if (!workflowInfo.HasTemplateFile || string.IsNullOrWhiteSpace(workflowInfo.PdfTemplatePath))
            {
                throw new UserFriendlyException(L["WorkflowTemplateHasNoFile"]);
            }

            // Try to find existing file that matches the template path
            var existingTemplateFile = existingDocFiles
                .Where(f => f.Path == workflowInfo.PdfTemplatePath && !f.IsSigned)
                .OrderByDescending(f => f.UploadedAt)
                .FirstOrDefault();

            if (existingTemplateFile != null)
            {
                // Reuse existing file - no duplicate created
                newSigningFileId = existingTemplateFile.Id;
            }
            else
            {
                // Template file not found on document (edge case) - create new one
                var templateFileName = Path.GetFileName(workflowInfo.PdfTemplatePath);
                var documentFile = new DocumentFile(
                    GuidGenerator.Create(),
                    documentId,
                    templateFileName,
                    false,
                    Clock.Now,
                    workflowInfo.PdfTemplatePath,
                    null
                );
                documentFile.TenantId = CurrentTenant.Id;
                await _documentFileRepository.InsertAsync(documentFile);
                newSigningFileId = documentFile.Id;
            }
        }
        else if (input.DocumentFileId.HasValue)
        {
            newSigningFileId = input.DocumentFileId.Value;
        }

        // 4. If user wants to update the document (change to a different personal document)
        // BUG-6 FIX: Reset old document status since it's no longer in any active workflow.
        if (input.NewDocumentId.HasValue && input.NewDocumentId.Value != documentId)
        {
            await UpdateDocumentStatusAsync(documentId, DocumentStatusCode.DA_GUI);
            documentId = input.NewDocumentId.Value;
            // Update the instance to point to the new document
            returnedInstance.DocumentId = documentId;
        }

        // 5. Cleanup old RETURNED instance's assignments (mark as not current)
        var oldAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == returnedInstance.DocumentId
            && (x.Status == nameof(DocumentAssignmentStatus.REJECTED)
                || x.Status == nameof(DocumentAssignmentStatus.REVOKE)));

        foreach (var oldAssignment in oldAssignments)
        {
            oldAssignment.IsCurrent = false;
            await _documentAssignmentRepository.UpdateAsync(oldAssignment);
        }

        // 6. Get workflow info
        var submitInfo = await GetWorkflowSubmitInfoAsync(returnedInstance.WorkflowId);
        var allStepsOrdered = submitInfo.Steps.OrderBy(s => s.Order).ToList();
        var firstStep = allStepsOrdered.First();
        var isParallel = submitInfo.SignMode == nameof(SignMode.PARALLEL);

        if (!firstStep.AssignedUsers.Any())
        {
            throw new UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        if (isParallel)
        {
            foreach (var step in allStepsOrdered)
            {
                if (!step.AssignedUsers.Any())
                    throw new UserFriendlyException(L["AllStepsMustHaveAssignedUsers"]);
            }
        }

        var nowTime = Clock.Now;

        // 7. Calculate FinishedAt (deadline)
        DateTime finishedAt;
        if (isParallel)
        {
            var maxSlaDays = allStepsOrdered
                .Where(s => s.SLADays.HasValue)
                .Select(s => s.SLADays!.Value)
                .DefaultIfEmpty(0)
                .Max();
            finishedAt = maxSlaDays > 0 ? nowTime.AddDays(maxSlaDays) : DateTime.MinValue;
        }
        else
        {
            finishedAt = firstStep.SLADays.HasValue
                ? nowTime.AddDays(firstStep.SLADays.Value)
                : DateTime.MinValue;
        }

        // 8. ISSUE-2 FIX: REUSE the same workflow instance instead of creating a new one.
        // This preserves all workflow logs history from previous submissions.
        returnedInstance.Status = nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
        returnedInstance.CurrentStepId = firstStep.StepId;
        returnedInstance.StartedAt = nowTime;
        returnedInstance.FinishedAt = finishedAt;
        await _documentWorkflowInstanceRepository.UpdateAsync(returnedInstance);

        // 9. Resolve signing file
        Guid? signingFileId = newSigningFileId;
        if (signingFileId == null)
        {
            var documentFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == documentId);
            signingFileId = documentFiles.OrderByDescending(f => f.UploadedAt).FirstOrDefault()?.Id;
        }

        // 10. Create DocumentAssignments for step 1 (or all steps if PARALLEL)
        var stepsToAssign = isParallel ? allStepsOrdered : new List<WorkflowStepDetailDto> { firstStep };
        var allNotifyUserIds = new List<Guid>();

        foreach (var step in stepsToAssign)
        {
            Guid? stepFileId = signingFileId;
            if (isParallel && step.Order > firstStep.Order)
            {
                stepFileId = await CopyDocumentFileForNextStepAsync(signingFileId, documentId);
                if (!stepFileId.HasValue)
                {
                    Logger.LogError("[RE_SUBMIT] CopyDocumentFileForNextStepAsync returned null for parallel step {StepOrder}. SigningFileId={SigningFileId}, DocumentId={DocumentId}",
                        step.Order, signingFileId, documentId);
                    throw new UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
                }
            }

            foreach (var user in step.AssignedUsers)
            {
                await _documentAssignmentManager.CreateAsync(
                    documentId,
                    step.StepId,
                    user.UserId,
                    step.Order,
                    step.Type,
                    nameof(DocumentAssignmentStatus.PENDING),
                    nowTime,
                    DateTime.MinValue,
                    true,
                    stepFileId
                );
                allNotifyUserIds.Add(user.UserId);
            }
        }

        // 11. Create DocumentHistory records
        foreach (var step in stepsToAssign)
        {
            foreach (var user in step.AssignedUsers)
            {
                await _documentHistoryManager.CreateAsync(
                    documentId,
                    CurrentUser.Id,
                    user.UserId,
                    nameof(DocumentHistoryAction.TRINH),
                    input.SigningContent
                );
            }
        }

        // 12. Add RE_SUBMIT log to the SAME workflow instance (preserves history)
        await _documentWorkflowInstanceLogsManager.CreateAsync(
            returnedInstance.Id, // Same instance ID - logs stay together
            null,
            CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW),
            WorkflowConstants.RoleInitiator,
            nameof(DocumentWorkflowInstanceStatus.RETURNED),
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            "RE_SUBMIT"
        );

        // 13. ISSUE-3 FIX: Only attach USER-UPLOADED files, NOT the signing PDF.
        // The signing PDF is already referenced through DocumentAssignment.DocumentFileResultId.
        if (input.AttachedFileIds != null && input.AttachedFileIds.Any())
        {
            foreach (var fileId in input.AttachedFileIds)
            {
                var instanceFile = new DocumentWorkflowInstanceFile(
                    GuidGenerator.Create(),
                    returnedInstance.Id, // Same instance
                    fileId
                );
                instanceFile.TenantId = CurrentTenant.Id;
                await _documentWorkflowInstanceFileRepository.InsertAsync(instanceFile);
            }
        }

        // 14. Delete old attached files if requested
        // BUG-1 FIX: Remove DocumentWorkflowInstanceFile references BEFORE deleting DocumentFile
        if (input.DeleteFileIds != null && input.DeleteFileIds.Any())
        {
            foreach (var fileId in input.DeleteFileIds)
            {
                // First, remove any DocumentWorkflowInstanceFile records referencing this file
                var referencingInstanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
                    x => x.DocumentFileId == fileId);
                foreach (var refFile in referencingInstanceFiles)
                {
                    await _documentWorkflowInstanceFileRepository.DeleteAsync(refFile);
                }

                // Then safely delete the DocumentFile
                var file = await _documentFileRepository.FindAsync(fileId);
                if (file != null)
                {
                    await _documentFileRepository.DeleteAsync(file);
                }
            }
        }

        // 15. Send notification
        var doc = await _documentRepository.GetAsync(documentId);
        var distinctNotifyUserIds = allNotifyUserIds.Distinct().ToList();
        if (distinctNotifyUserIds.Any())
        {
            await SendWorkflowNotificationAsync(
                doc,
                distinctNotifyUserIds,
                "WorkflowResubmitted",
                $"WorkflowResubmittedMessage|{doc.StorageNumber}|{doc.Title}|{submitInfo.WorkflowName}|{firstStep.Name}"
            );
        }

        // 16. Update document status back to DANG_XU_LY
        await UpdateDocumentStatusAsync(documentId, DocumentStatusCode.DANG_XU_LY);

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(returnedInstance);
    }

    /// <summary>
    /// Get info for a returned workflow instance so the UI can pre-populate the re-submit modal.
    /// Returns workflow info, original signing content, attached files, etc.
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task<ReturnedWorkflowInfoDto> GetReturnedWorkflowInfoAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.RETURNED))
        {
            throw new UserFriendlyException(L["WorkflowNotReturned"]);
        }

        // Get the original document
        var document = await _documentRepository.GetAsync(instance.DocumentId);

        // Get workflow info
        var workflowInfo = await GetWorkflowSubmitInfoAsync(instance.WorkflowId);

        // Get the last signing content from DocumentHistory
        var histories = await _documentHistoryRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId);
        var lastHistory = histories
            .OrderByDescending(h => h.CreationTime)
            .FirstOrDefault();

        // Get user-uploaded attached files from the workflow instance (DocumentWorkflowInstanceFile)
        var instanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id);
        var attachedFileIds = instanceFiles.Select(f => f.DocumentFileId).ToList();
        var attachedFiles = attachedFileIds.Any()
            ? await _documentFileRepository.GetListAsync(x => attachedFileIds.Contains(x.Id))
            : new List<DocumentFile>();

        // Get document's own files (signing PDF, template file, etc.)
        var documentFiles = await _documentFileRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId);

        return new ReturnedWorkflowInfoDto
        {
            WorkflowInstanceId = instance.Id,
            DocumentId = instance.DocumentId,
            WorkflowId = instance.WorkflowId,
            DocumentTitle = document.Title,
            DocumentNo = document.No,
            StorageNumber = document.StorageNumber,
            LastSigningContent = lastHistory?.Comment,
            WorkflowInfo = workflowInfo,
            AttachedFiles = attachedFiles.Select(f => new AttachedFileDto
            {
                FileId = f.Id,
                FileName = f.Name,
                FilePath = f.Path
            }).ToList(),
            DocumentFiles = documentFiles
                .OrderByDescending(f => f.UploadedAt)
                .Select(f => new AttachedFileDto
                {
                    FileId = f.Id,
                    FileName = f.Name,
                    FilePath = f.Path
                }).ToList()
        };
    }

    #endregion

    #region GetAllStepsWithStatusAsync

    /// <summary>
    /// Get all workflow steps with their signing status for the action modal.
    /// Shows each step name, assigned users, and whether they have signed (with signing index).
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);

        // Get all steps for the workflow template
        var allSteps = await _workflowStepTemplateRepository.GetListAsync(
            x => x.WorkflowTemplateId == instance.WorkflowTemplateId && x.IsActive);
        allSteps = allSteps.OrderBy(s => s.Order).ToList();

        // Get all step assignments (for step user info)
        var stepIds = allSteps.Select(s => s.Id).ToList();
        var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && stepIds.Contains(x.StepId.Value) && x.IsActive);

        // Get all document assignments for this document (current workflow pass)
        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.CreationTime >= instance.StartedAt);

        // Batch load all involved user IDs
        var allUserIds = stepAssignments
            .Where(sa => sa.DefaultUserId.HasValue)
            .Select(sa => sa.DefaultUserId!.Value)
            .Union(docAssignments.Select(a => a.ReceiverUserId))
            .Distinct()
            .ToList();
        var users = await _identityUserRepository.GetListAsync(x => allUserIds.Contains(x.Id));
        var userDict = users.ToDictionary(u => u.Id);

        var result = new List<WorkflowStepStatusDto>();

        foreach (var step in allSteps)
        {
            var stepDto = new WorkflowStepStatusDto
            {
                StepId = step.Id,
                Order = step.Order,
                Name = step.Name,
                Type = step.Type,
                IsCurrentStep = instance.CurrentStepId == step.Id,
                Users = new List<StepAssignmentUserDto>()
            };

            // Get step's assigned users from step assignments
            var thisStepAssignments = stepAssignments.Where(sa => sa.StepId == step.Id).ToList();

            // Check document assignments for this step
            var thisStepDocAssignments = docAssignments
                .Where(a => a.WorkflowStepTemplateId == step.Id)
                .ToList();

            bool hasCompletedUser = false;

            foreach (var sa in thisStepAssignments)
            {
                if (!sa.DefaultUserId.HasValue) continue;

                var userId = sa.DefaultUserId.Value;
                userDict.TryGetValue(userId, out var user);

                // Find the document assignment for this user at this step
                var docAssignment = thisStepDocAssignments
                    .FirstOrDefault(a => a.ReceiverUserId == userId);

                var userStatus = docAssignment?.Status;
                if (userStatus == nameof(DocumentAssignmentStatus.DONE))
                {
                    hasCompletedUser = true;
                }

                stepDto.Users.Add(new StepAssignmentUserDto
                {
                    UserId = userId,
                    FullName = user != null ? $"{user.Surname} {user.Name}".Trim() : null,
                    UserName = user?.UserName,
                    IsPrimary = sa.IsPrimary,
                    Status = userStatus,
                    ProcessedAt = docAssignment?.ProcessedAt > DateTime.MinValue ? docAssignment.ProcessedAt : null,
                    // SigningIndex = step order (matches placeholder <<Sign{NN}>> in PDF)
                    SigningIndex = userStatus == nameof(DocumentAssignmentStatus.DONE) ? step.Order : null
                });
            }

            stepDto.IsCompleted = hasCompletedUser;
            result.Add(stepDto);
        }

        return result;
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
    /// ISSUE-06 FIX: Refactored to use queryable JOINs at DB level instead of loading all
    /// assignments/documents into memory. Filter, count, and page are done in SQL.
    /// 
    /// Logic:
    ///   All = Document where user is receiver OR creator of any assignment
    ///   SentToMe = DocumentAssignment.ReceiverUserId = currentUserId
    ///   SentByMe = DocumentAssignment.CreatorId = currentUserId
    ///   Following = empty (no logic for now)
    /// </summary>
    public async Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input)
    {
        var currentUserId = CurrentUser.Id!.Value;

        // ===== STEP 1: Build queryable for distinct document IDs per category at DB level =====
        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var documentQueryable = await _documentRepository.GetQueryableAsync();

        // Distinct document IDs where user is receiver (DB query, not materialized yet)
        var receivedDocIdQuery = assignmentQueryable
            .Where(a => a.ReceiverUserId == currentUserId)
            .Select(a => a.DocumentId)
            .Distinct();

        // Distinct document IDs where user is creator (DB query, not materialized yet)
        var createdDocIdQuery = assignmentQueryable
            .Where(a => a.CreatorId == currentUserId)
            .Select(a => a.DocumentId)
            .Distinct();

        // All = union of received + created
        var allDocIdQuery = receivedDocIdQuery.Union(createdDocIdQuery);

        // ===== STEP 2: Build filtered document base query at DB level =====
        var baseDocQuery = documentQueryable.Where(d => allDocIdQuery.Contains(d.Id));

        // Apply date filter at DB level
        if (input.FromDate.HasValue)
        {
            var fromDate = input.FromDate.Value.Date;
            baseDocQuery = baseDocQuery.Where(d => d.IncommingDate >= fromDate);
        }
        if (input.ToDate.HasValue)
        {
            var toDateEnd = input.ToDate.Value.Date.AddDays(1).AddSeconds(-1);
            baseDocQuery = baseDocQuery.Where(d => d.IncommingDate <= toDateEnd);
        }

        // Apply text filter at DB level
        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            var filterText = input.FilterText.Trim();
            baseDocQuery = baseDocQuery.Where(d =>
                (d.Title != null && d.Title.Contains(filterText)) ||
                (d.No != null && d.No.Contains(filterText)) ||
                (d.StorageNumber != null && d.StorageNumber.Contains(filterText)));
        }

        // ===== STEP 3: Count per category at DB level =====
        var filteredDocIds = baseDocQuery.Select(d => d.Id);

        int sentToMeCount = await AsyncExecuter.CountAsync(
            filteredDocIds.Where(id => receivedDocIdQuery.Contains(id)));
        int sentByMeCount = await AsyncExecuter.CountAsync(
            filteredDocIds.Where(id => createdDocIdQuery.Contains(id)));
        int followingCount = 0; // No logic for now
        int allCount = await AsyncExecuter.CountAsync(filteredDocIds);

        // ===== STEP 4: Apply filter mode at DB level =====
        IQueryable<Document> modeFilteredQuery;
        switch (input.FilterMode)
        {
            case DocumentSigningFilterMode.SentToMe:
                modeFilteredQuery = baseDocQuery.Where(d => receivedDocIdQuery.Contains(d.Id));
                break;
            case DocumentSigningFilterMode.SentByMe:
                modeFilteredQuery = baseDocQuery.Where(d => createdDocIdQuery.Contains(d.Id));
                break;
            case DocumentSigningFilterMode.Following:
                // Return empty result for Following mode
                return new DocumentSigningPageResultDto
                {
                    TotalCount = 0,
                    Items = new List<DocumentSigningItemDto>(),
                    AllCount = allCount,
                    SentToMeCount = sentToMeCount,
                    SentByMeCount = sentByMeCount,
                    FollowingCount = followingCount
                };
            default: // All
                modeFilteredQuery = baseDocQuery;
                break;
        }

        // ===== STEP 5: Count + page at DB level =====
        var totalCount = await AsyncExecuter.CountAsync(modeFilteredQuery);
        var pagedDocuments = await AsyncExecuter.ToListAsync(
            modeFilteredQuery
                .OrderByDescending(d => d.IncommingDate)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        if (!pagedDocuments.Any())
        {
            return new DocumentSigningPageResultDto
            {
                TotalCount = totalCount,
                Items = new List<DocumentSigningItemDto>(),
                AllCount = allCount,
                SentToMeCount = sentToMeCount,
                SentByMeCount = sentByMeCount,
                FollowingCount = followingCount
            };
        }

        // ===== STEP 6: Batch load related data for paged documents only =====
        var pagedDocIds = pagedDocuments.Select(d => d.Id).ToList();

        // Load current user's assignments for the paged documents only (for CanAct/MyAssignment)
        var myAssignments = await AsyncExecuter.ToListAsync(
            assignmentQueryable.Where(a => pagedDocIds.Contains(a.DocumentId) && a.ReceiverUserId == currentUserId));

        // Get all workflow instances for the paged documents
        var allInstances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => pagedDocIds.Contains(x.DocumentId));

        // Batch load MasterData for StatusId + TypeId
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

        // Batch load Workflows referenced by instances
        var workflowIds = allInstances.Select(i => i.WorkflowId).Distinct().ToList();
        var workflowDict = workflowIds.Any()
            ? (await _workflowRepository.GetListAsync(x => workflowIds.Contains(x.Id)))
                .ToDictionary(w => w.Id, w => w)
            : new Dictionary<Guid, Workflow>();

        // Batch load current steps (WorkflowStepTemplates) referenced by instances
        var stepIds = allInstances.Select(i => i.CurrentStepId).Distinct().ToList();
        var stepDict = stepIds.Any()
            ? (await _workflowStepTemplateRepository.GetListAsync(x => stepIds.Contains(x.Id)))
                .ToDictionary(s => s.Id, s => s)
            : new Dictionary<Guid, WorkflowStepTemplate>();

        // Batch load total step counts per WorkflowTemplate
        var templateIds = allInstances.Select(i => i.WorkflowTemplateId).Distinct().ToList();
        var allStepsForTemplates = templateIds.Any()
            ? await _workflowStepTemplateRepository.GetListAsync(
                x => templateIds.Contains(x.WorkflowTemplateId) && x.IsActive)
            : new List<WorkflowStepTemplate>();
        var totalStepsDict = allStepsForTemplates
            .GroupBy(s => s.WorkflowTemplateId)
            .ToDictionary(g => g.Key, g => g.Count());

        // ===== STEP 7: BUILD ITEMS (no more DB calls in loop) =====
        var items = new List<DocumentSigningItemDto>();
        foreach (var doc in pagedDocuments)
        {
            // Get the latest (or active IN_PROGRESS) instance for this document
            var docInstance = allInstances
                .Where(x => x.DocumentId == doc.Id)
                .OrderByDescending(x => x.StartedAt)
                .FirstOrDefault();

            var myDocAssignment = myAssignments
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
                MyAssignmentId = myDocAssignment?.Id,
                // CanResubmit: workflow was returned AND current user is the initiator
                CanResubmit = docInstance != null
                    && docInstance.Status == nameof(DocumentWorkflowInstanceStatus.RETURNED)
                    && docInstance.CreatorId == currentUserId
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
                WorkflowConstants.PriorityHigh,
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
    /// RISK-2 FIX: Pure READ-ONLY check. No write operations.
    /// The BackgroundWorker (WorkflowOverdueBackgroundWorker) handles the actual cancellation.
    /// 
    /// This method only checks:
    /// 1. Whether the workflow instance is overdue (FinishedAt <= now and not terminal)
    /// 2. Whether the workflow has already been cancelled (terminal status)
    /// 3. Whether the current step allows the Return action
    /// 
    /// Security: Verifies the calling user is related to this workflow instance
    /// (either as creator or as an assignment receiver) before allowing the check.
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

            // ISSUE-05 FIX: Authorization - verify user is related to this workflow instance
            var currentUserId = CurrentUser.Id!.Value;
            var isCreator = instance.CreatorId == currentUserId;
            if (!isCreator)
            {
                var hasAssignment = await _documentAssignmentRepository.AnyAsync(
                    x => x.DocumentId == instance.DocumentId && x.ReceiverUserId == currentUserId);
                if (!hasAssignment)
                {
                    Logger.LogWarning(
                        "[OVERDUE_AUTH] User {UserId} attempted to check overdue for workflow {InstanceId} " +
                        "but is not creator or assignment receiver.",
                        currentUserId, workflowInstanceId);
                    throw new UserFriendlyException(L["NotAuthorizedForThisAction"]);
                }
            }

            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            result.AllowReturn = currentStep.AllowReturn;

            // Terminal statuses - workflow is already finished
            var terminalStatuses = new[]
            {
                nameof(DocumentWorkflowInstanceStatus.COMPLETED),
                nameof(DocumentWorkflowInstanceStatus.REJECTED),
                nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                nameof(DocumentWorkflowInstanceStatus.RETURNED)
            };

            // If already in terminal status (e.g. cancelled by BackgroundWorker), report as overdue
            if (instance.Status == nameof(DocumentWorkflowInstanceStatus.CANCELLED))
            {
                result.IsOverdue = true;
            }
            // If FinishedAt is set and has passed, and status is not terminal → overdue detected
            // The BackgroundWorker will handle the actual cancellation within its next cycle
            else if (instance.FinishedAt > DateTime.MinValue
                && instance.FinishedAt <= Clock.Now
                && !terminalStatuses.Contains(instance.Status))
            {
                result.IsOverdue = true;
                // No write operations here - BackgroundWorker handles cancellation
                Logger.LogInformation(
                    "[OVERDUE_CHECK] Workflow {InstanceId} is overdue (FinishedAt={FinishedAt}). " +
                    "BackgroundWorker will handle cancellation.",
                    workflowInstanceId, instance.FinishedAt);
            }
        }
        catch (UserFriendlyException)
        {
            throw; // Re-throw authorization exceptions to the UI
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error checking overdue for workflow instance {InstanceId}", workflowInstanceId);
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
                instance.FinishedAt = Clock.Now; // ISSUE-08 FIX
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
                    WorkflowConstants.RoleSystem,
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
    /// 
    /// ISSUE-02 FIX: Throws UserFriendlyException on copy failure instead of returning
    /// the source file ID. Returning the source ID would allow the next step to overwrite
    /// the original signed file, destroying the audit trail.
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

        if (!sourceFileId.HasValue)
        {
            Logger.LogWarning("[COPY_FILE] sourceFileId is null and no fallback found. DocumentId={DocumentId}", documentId);
            return null;
        }

        var sourceFile = await _documentFileRepository.FindAsync(sourceFileId.Value);
        if (sourceFile == null || string.IsNullOrEmpty(sourceFile.Path))
        {
            Logger.LogWarning("[COPY_FILE] Source file not found or has no path. SourceFileId={SourceFileId}, DocumentId={DocumentId}, Found={Found}, Path={Path}",
                sourceFileId.Value, documentId, sourceFile != null, sourceFile?.Path);
            return null;
        }

        try
        {
            // Read the source blob
            var fileBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);

            // Create new blob path
            var extension = Path.GetExtension(sourceFile.Name);
            var newBlobPath = $"{WorkflowConstants.BlobPathSigningSteps}{Guid.NewGuid()}{extension}";

            // Upload to new path in blob storage
            await _blobContainer.SaveAsync(newBlobPath, fileBytes);

            // Create new DocumentFile record
            var newFile = new DocumentFile(
                GuidGenerator.Create(),
                null, //documentId
                sourceFile.Name,
                sourceFile.IsSigned,
                Clock.Now, // ISSUE-08 FIX
                newBlobPath,
                sourceFile.Hash
            );
            newFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(newFile);

            return newFile.Id;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex,
                "Error copying document file for next step. SourceFileId={SourceFileId}, DocumentId={DocumentId}, SourcePath={SourcePath}",
                sourceFileId, documentId, sourceFile.Path);
            // ISSUE-02 FIX: Do NOT return sourceFileId as fallback.
            // Returning the original file ID would cause the next step's signing to overwrite
            // the already-signed file, destroying the audit trail and previous step's signature.
            throw new UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
        }
    }

    #endregion

    #region Electronic Signing (Ký điện tử)

    /// <summary>
    /// Apply electronic signature to the PDF document for the given assignment.
    /// This method is designed to be reusable for digital signing in the future (same parameters).
    /// 
    /// Flow:
    /// 1. Validate user has an active, valid electronic signature (UserSignature)
    /// 2. Get user's full name (Surname + Name) from IdentityUser
    /// 3. Read the current PDF file from the assignment's DocumentFileResultId
    /// 4. Replace placeholders based on step order:
    ///    User at step N replaces &lt;&lt;Sign{N:D2}&gt;&gt;, &lt;&lt;FullName{N:D2}&gt;&gt;, &lt;&lt;NoteContent{N:D2}&gt;&gt;
    ///    This ensures each step's placeholder area in the PDF template is correctly matched.
    /// 5. Upload signed PDF to Minio as a new file
    /// 6. Create new DocumentFile record (IsSigned = true)
    /// 7. Update assignment's DocumentFileResultId to reference the signed file
    /// </summary>
    private async Task ApplyElectronicSignatureAsync(
        DocumentAssignment assignment,
        DocumentWorkflowInstance instance,
        string? noteContent)
    {
        var currentUserId = CurrentUser.Id!.Value;

        // ==================== STEP 1: Validate user's electronic signature ====================
        Logger.LogInformation("[ELECTRONIC_SIGN] Starting electronic signing for user {UserId}, assignment {AssignmentId}, stepOrder={StepOrder}",
            currentUserId, assignment.Id, assignment.StepOrder);

        UserSignature? signature;
        try
        {
            // ISSUE-12 FIX: Query only the current user's signature instead of loading ALL signatures
            var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
            signature = await AsyncExecuter.FirstOrDefaultAsync(
                sigQueryable.Where(s => s.IdentityUserId == currentUserId
                    && s.SignType == nameof(SignType.ELECTRONIC)
                    && s.IsActive));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error querying user signatures for user {UserId}", currentUserId);
            throw new UserFriendlyException(L["ElectronicSigningFailed", ex.Message]);
        }

        if (signature == null)
        {
            Logger.LogError("[ELECTRONIC_SIGN] User has no electronic signature. UserId={UserId}", currentUserId);
            throw new UserFriendlyException(L["UserHasNoElectronicSignature"]);
        }

        // Check if signature is activated
        if (!signature.IsActive)
        {
            Logger.LogError("[ELECTRONIC_SIGN] Signature not activated. SignatureId={SignatureId}", signature.Id);
            throw new UserFriendlyException(L["SignatureNotActivated"]);
        }

        // Check signature image is configured
        if (string.IsNullOrWhiteSpace(signature.SignatureImage))
        {
            Logger.LogError("[ELECTRONIC_SIGN] SignatureImage not configured. SignatureId={SignatureId}", signature.Id);
            throw new UserFriendlyException(L["SignatureImageNotConfigured"]);
        }

        // Check validity period
        var now = Clock.Now; // ISSUE-08 FIX
        if (signature.ValidFrom.HasValue && signature.ValidFrom.Value > now)
        {
            Logger.LogError("[ELECTRONIC_SIGN] Signature not yet valid. SignatureId={SignatureId}", signature.Id);
            throw new UserFriendlyException(L["SignatureNotYetValid"]);
        }
        if (signature.ValidTo.HasValue && signature.ValidTo.Value < now)
        {
            Logger.LogError("[ELECTRONIC_SIGN] Signature expired. SignatureId={SignatureId}", signature.Id);
            throw new UserFriendlyException(L["SignatureExpired"]);
        }

        Logger.LogInformation("[ELECTRONIC_SIGN] User signature validated. SignatureId={SignatureId}", signature.Id);

        // ==================== STEP 2: Get user's full name ====================
        string fullName;
        try
        {
            var user = await _identityUserRepository.GetAsync(currentUserId);
            fullName = $"{user.Surname} {user.Name}".Trim();
            if (string.IsNullOrWhiteSpace(fullName))
            {
                fullName = user.UserName ?? "Unknown";
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error getting user info for user {UserId}", currentUserId);
            throw new UserFriendlyException(L["ElectronicSigningFailed", "Cannot get user information"]);
        }

        // ==================== STEP 3: Read the current PDF file ====================
        if (!assignment.DocumentFileResultId.HasValue)
        {
            Logger.LogError("[ELECTRONIC_SIGN] No file to sign. AssignmentId={AssignmentId}", assignment.Id);
            throw new UserFriendlyException(L["NoFileToSign"]);
        }

        DocumentFile sourceFile;
        byte[] pdfBytes;
        try
        {
            Logger.LogInformation("[ELECTRONIC_SIGN] Getting source file. AssignmentId={AssignmentId}", assignment.Id);
            sourceFile = await _documentFileRepository.GetAsync(assignment.DocumentFileResultId.Value);
            if (string.IsNullOrEmpty(sourceFile.Path))
            {
                Logger.LogError("[ELECTRONIC_SIGN] Source file not found. AssignmentId={AssignmentId}", assignment.Id);
                throw new UserFriendlyException(L["NoFileToSign"]);
            }
            Logger.LogInformation("[ELECTRONIC_SIGN] Getting source file bytes. AssignmentId={AssignmentId}", assignment.Id);
            pdfBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
        }
        catch (UserFriendlyException ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error getting source file. AssignmentId={AssignmentId}", assignment.Id);
            throw; // Re-throw user-friendly exceptions as-is
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error reading source PDF. FileId={FileId}", assignment.DocumentFileResultId);
            throw new UserFriendlyException(L["NoFileToSign"]);
        }

        Logger.LogInformation("[ELECTRONIC_SIGN] Source PDF loaded. FileId={FileId}, Size={Size} bytes, Path={Path}",
            sourceFile.Id, pdfBytes.Length, sourceFile.Path);

        // ==================== STEP 4: Get signature image bytes ====================
        byte[] signatureImageBytes;
        try
        {
            signatureImageBytes = await ResolveSignatureImageBytesAsync(signature.SignatureImage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error resolving signature image for user {UserId}", currentUserId);
            throw new UserFriendlyException(L["ErrorReadingSignatureImage"]);
        }

        // ==================== STEP 5: Replace placeholders in PDF ====================
        // Use assignment.StepOrder to match the correct placeholder in the PDF template.
        // User at step N → replaces <<Sign{N:D2}>>, <<FullName{N:D2}>>, <<NoteContent{N:D2}>>
        byte[] signedPdfBytes;
        try
        {
            signedPdfBytes = ReplacePdfPlaceholders(
                pdfBytes,
                assignment.StepOrder,
                signatureImageBytes,
                fullName,
                noteContent ?? "");
        }
        catch (UserFriendlyException)
        {
            throw; // Re-throw user-friendly exceptions
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error replacing PDF placeholders. StepOrder={StepOrder}", assignment.StepOrder);
            throw new UserFriendlyException(L["ErrorProcessingPdf", ex.Message]);
        }

        Logger.LogInformation("[ELECTRONIC_SIGN] PDF placeholders replaced. OriginalSize={OriginalSize}, SignedSize={SignedSize}",
            pdfBytes.Length, signedPdfBytes.Length);

        // ==================== STEP 6: Upload signed PDF to Minio ====================
        string newBlobPath;
        try
        {
            var extension = Path.GetExtension(sourceFile.Name);
            if (string.IsNullOrEmpty(extension)) extension = ".pdf";
            newBlobPath = $"{WorkflowConstants.BlobPathElectronicSigned}{Guid.NewGuid()}{extension}";
            await _blobContainer.SaveAsync(newBlobPath, signedPdfBytes);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error uploading signed PDF to blob storage");
            throw new UserFriendlyException(L["ElectronicSigningFailed", "Cannot upload signed file"]);
        }

        // ==================== STEP 7: Create new DocumentFile record ====================
        DocumentFile signedFile;
        try
        {
            var hash = Convert.ToBase64String(System.Security.Cryptography.SHA256.HashData(signedPdfBytes));
            signedFile = new DocumentFile(
                GuidGenerator.Create(),
                null, // documentId - linked via assignment, not directly to document
                sourceFile.Name, // Keep original file name
                true, // IsSigned = true
                now,
                newBlobPath,
                hash
            );
            signedFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(signedFile, autoSave: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error creating signed DocumentFile record");
            throw new UserFriendlyException(L["ElectronicSigningFailed", "Cannot save signed file record"]);
        }

        // ==================== STEP 8: Update assignment's DocumentFileResultId ====================
        try
        {
            assignment.DocumentFileResultId = signedFile.Id;
            await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error updating assignment DocumentFileResultId. AssignmentId={AssignmentId}", assignment.Id);
            throw new UserFriendlyException(L["ElectronicSigningFailed", "Cannot update assignment"]);
        }

        Logger.LogInformation(
            "[ELECTRONIC_SIGN] Electronic signature applied successfully. AssignmentId={AssignmentId}, SignedFileId={SignedFileId}, BlobPath={BlobPath}",
            assignment.Id, signedFile.Id, newBlobPath);
    }

    #endregion

    #region PDF Placeholder Replacement Helpers

    /// <summary>
    /// Internal class to hold placeholder position information found by PdfPig
    /// </summary>
    private class PlaceholderPosition
    {
        public int PageIndex { get; set; }
        /// <summary>X coordinate in PdfPig coords (bottom-left origin)</summary>
        public double X { get; set; }
        /// <summary>Top Y coordinate in PdfPig coords (bottom-left origin, Y increases upward)</summary>
        public double YTop { get; set; }
        /// <summary>Bottom Y coordinate in PdfPig coords</summary>
        public double YBottom { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double FontSize { get; set; }
        public string Type { get; set; } = ""; // "SIGN", "FULLNAME", "NOTE"
    }

    /// <summary>
    /// Replace placeholders in a PDF file with signature image, full name, and note content.
    /// Uses PdfPig to find placeholder positions, then PDFsharp to overlay replacement content.
    /// 
    /// Placeholders follow the pattern: &lt;&lt;Sign{NN}&gt;&gt;, &lt;&lt;FullName{NN}&gt;&gt;, &lt;&lt;NoteContent{NN}&gt;&gt;
    /// where NN is the step order zero-padded to 2 digits (01, 02, 03...).
    /// User at step N replaces placeholder Sign{N}, ensuring each step's designated area is matched.
    /// </summary>
    private byte[] ReplacePdfPlaceholders(
        byte[] pdfBytes,
        int stepOrder,
        byte[] signatureImageBytes,
        string fullName,
        string noteContent)
    {
        var suffix = stepOrder.ToString("D2"); // "01", "02", "03", ...
        var signTag = $"<<Sign{suffix}>>";
        var nameTag = $"<<FullName{suffix}>>";
        var noteTag = $"<<NoteContent{suffix}>>";

        // STEP 1: Use PdfPig to find placeholder positions
        var positions = new List<PlaceholderPosition>();
        double[] pageHeights;

        using (var pdfPigDoc = UglyToad.PdfPig.PdfDocument.Open(pdfBytes))
        {
            pageHeights = new double[pdfPigDoc.NumberOfPages];

            for (int p = 0; p < pdfPigDoc.NumberOfPages; p++)
            {
                var page = pdfPigDoc.GetPage(p + 1);
                pageHeights[p] = page.Height;

                var letters = page.Letters.ToList();
                if (!letters.Any()) continue;

                // Concatenate all letter values to build the full text of the page
                var fullText = string.Concat(letters.Select(l => l.Value));

                // Log extracted text for debugging (first 500 chars)
                var textPreview = fullText.Length > 500 ? fullText[..500] + "..." : fullText;
                Logger.LogInformation("[PDF_REPLACE] Page {Page}: Extracted {LetterCount} letters, text preview: '{TextPreview}'",
                    p + 1, letters.Count, textPreview);

                // Search for each placeholder tag in the concatenated text
                var searchPairs = new[]
                {
                    (Tag: signTag, Type: "SIGN"),
                    (Tag: nameTag, Type: "FULLNAME"),
                    (Tag: noteTag, Type: "NOTE")
                };

                foreach (var (tag, type) in searchPairs)
                {
                    var index = fullText.IndexOf(tag, StringComparison.Ordinal);
                    if (index < 0)
                    {
                        Logger.LogWarning("[PDF_REPLACE] Placeholder '{Tag}' NOT found on page {Page}. Trying case-insensitive...", tag, p + 1);

                        // Try case-insensitive search as fallback
                        index = fullText.IndexOf(tag, StringComparison.OrdinalIgnoreCase);
                        if (index < 0)
                        {
                            Logger.LogWarning("[PDF_REPLACE] Placeholder '{Tag}' NOT found (case-insensitive) on page {Page}", tag, p + 1);
                            continue;
                        }
                    }

                    // Get the letters that form this placeholder
                    var placeholderLetters = letters.Skip(index).Take(tag.Length).ToList();
                    if (placeholderLetters.Count < tag.Length)
                    {
                        Logger.LogWarning("[PDF_REPLACE] Not enough letters for placeholder '{Tag}' at index {Index}. Expected {Expected}, got {Actual}",
                            tag, index, tag.Length, placeholderLetters.Count);
                        continue;
                    }

                    // Calculate bounding box from the letters
                    var minX = placeholderLetters.Min(l => l.GlyphRectangle.Left);
                    var minY = placeholderLetters.Min(l => l.GlyphRectangle.Bottom);
                    var maxX = placeholderLetters.Max(l => l.GlyphRectangle.Right);
                    var maxY = placeholderLetters.Max(l => l.GlyphRectangle.Top);
                    var fontSize = (double)placeholderLetters.First().FontSize;

                    positions.Add(new PlaceholderPosition
                    {
                        PageIndex = p,
                        X = minX,
                        YTop = maxY,
                        YBottom = minY,
                        Width = maxX - minX,
                        Height = maxY - minY,
                        FontSize = fontSize > 0 ? fontSize : 10,
                        Type = type
                    });

                    Logger.LogInformation("[PDF_REPLACE] Found placeholder '{Tag}' on page {Page} at ({X},{YBottom})-({MaxX},{YTop}), fontSize={FontSize}",
                        tag, p + 1, minX, minY, maxX, maxY, fontSize);
                }
            }
        }

        if (!positions.Any())
        {
            Logger.LogError("[PDF_REPLACE] NO PLACEHOLDERS FOUND for step order {StepOrder} (suffix={Suffix}). Tags searched: '{SignTag}', '{NameTag}', '{NoteTag}'. " +
                "This means the PDF text extracted by PdfPig does not contain these exact strings. " +
                "Check if the PDF uses different characters for << >> (e.g. Unicode angle brackets « » or ＜＜ ＞＞).",
                stepOrder, suffix, signTag, nameTag, noteTag);
            // Return original PDF if no placeholders found - don't throw,
            // because some steps might not have all placeholders
            return pdfBytes;
        }

        Logger.LogInformation("[PDF_REPLACE] Found {Count} placeholders for step order {StepOrder}. Proceeding with replacement...",
            positions.Count, stepOrder);

        // STEP 2: Use PDFsharp to overlay replacement content at found positions
        using var inputStream = new MemoryStream(pdfBytes);
        var document = PdfSharpIO.PdfReader.Open(inputStream, PdfSharpIO.PdfDocumentOpenMode.Modify);

        foreach (var pos in positions)
        {
            if (pos.PageIndex >= document.PageCount) continue;

            var page = document.Pages[pos.PageIndex];
            var gfx = PdfSharpDrawing.XGraphics.FromPdfPage(page, PdfSharpDrawing.XGraphicsPdfPageOptions.Append);

            // Convert from PdfPig coordinates (bottom-left origin) to PDFsharp XGraphics (top-left origin)
            // PDFsharp XGraphics Y = pageHeight - PdfPig Y_top
            var pgHeight = pageHeights[pos.PageIndex];
            double x = pos.X;
            double y = pgHeight - pos.YTop;
            double w = pos.Width;
            double h = pos.Height;

            // Draw white rectangle to cover the placeholder text
            var whiteRect = new PdfSharpDrawing.XRect(x, y, w, h);
            gfx.DrawRectangle(PdfSharpDrawing.XBrushes.White, whiteRect);

            switch (pos.Type)
            {
                case "SIGN":
                    // Draw signature image
                    if (signatureImageBytes.Length > 0)
                    {
                        try
                        {
                            using var imgStream = new MemoryStream(signatureImageBytes);
                            var img = PdfSharpDrawing.XImage.FromStream(imgStream);

                            // Scale image to fit placeholder area while maintaining aspect ratio
                            var imgAspect = (double)img.PixelWidth / img.PixelHeight;
                            var fitWidth = w;
                            var fitHeight = w / imgAspect;
                            if (fitHeight > h * 3) // Allow signature to be up to 3x placeholder height
                            {
                                fitHeight = h * 3;
                                fitWidth = fitHeight * imgAspect;
                            }

                            // Center the image on the placeholder position
                            var imgX = x;
                            var imgY = y - (fitHeight - h) / 2; // Center vertically around placeholder

                            gfx.DrawImage(img, imgX, imgY, fitWidth, fitHeight);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "[PDF_REPLACE] Error drawing signature image at page {Page}", pos.PageIndex + 1);
                            // Fallback: draw "SIGNED" text if image fails
                            var fallbackFont = new PdfSharpDrawing.XFont("Helvetica", pos.FontSize);
                            gfx.DrawString("[SIGNED]", fallbackFont, PdfSharpDrawing.XBrushes.Blue,
                                whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                        }
                    }
                    break;

                case "FULLNAME":
                    var nameFont = new PdfSharpDrawing.XFont("Helvetica", pos.FontSize);
                    gfx.DrawString(fullName, nameFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;

                case "NOTE":
                    var noteFont = new PdfSharpDrawing.XFont("Helvetica", Math.Max(pos.FontSize - 1, 8));
                    gfx.DrawString(noteContent, noteFont, PdfSharpDrawing.XBrushes.Black,
                        whiteRect, PdfSharpDrawing.XStringFormats.CenterLeft);
                    break;
            }

            gfx.Dispose();
        }

        using var outputStream = new MemoryStream();
        document.Save(outputStream);
        return outputStream.ToArray();
    }

    /// <summary>
    /// Resolve SignatureImage string to actual image bytes.
    /// Handles multiple formats:
    /// - Base64 data URI: "data:image/png;base64,iVBORw0KGgo..."
    /// - Plain base64 string: "iVBORw0KGgo..."
    /// - Blob storage path: "user-signatures/xxx.png"
    /// </summary>
    private async Task<byte[]> ResolveSignatureImageBytesAsync(string signatureImage)
    {
        if (string.IsNullOrWhiteSpace(signatureImage))
        {
            throw new UserFriendlyException(L["SignatureImageNotConfigured"]);
        }

        // Case 1: Base64 data URI (data:image/png;base64,...)
        if (signatureImage.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            var commaIndex = signatureImage.IndexOf(',');
            if (commaIndex > 0 && commaIndex < signatureImage.Length - 1)
            {
                var base64Data = signatureImage[(commaIndex + 1)..];
                return Convert.FromBase64String(base64Data);
            }
        }

        // Case 2: Plain base64 string (no path separators, valid base64 chars)
        if (!signatureImage.Contains('/') && !signatureImage.Contains('\\'))
        {
            try
            {
                return Convert.FromBase64String(signatureImage);
            }
            catch (FormatException)
            {
                // Not valid base64, fall through to blob storage
            }
        }

        // Case 3: Blob storage path
        try
        {
            return await _blobContainer.GetAllBytesAsync(signatureImage);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error reading signature image from blob storage. Path={Path}", signatureImage);
            throw new UserFriendlyException(L["ErrorReadingSignatureImage"]);
        }
    }

    #endregion

    #region Parallel Signing Merge (Gộp file ký song song)

    /// <summary>
    /// Merge all individually-signed PDFs from a PARALLEL workflow into one final document.
    /// 
    /// Strategy:
    /// - Get the ORIGINAL PDF (the first workflow instance file = template before any signing)
    /// - For each completed assignment (ordered by step order), re-apply that step's signature
    ///   to the original PDF. Since each step has unique placeholders (Sign01, Sign02, etc.),
    ///   they don't conflict and can all be applied sequentially to the same document.
    /// - Upload the merged result and update all assignments' DocumentFileResultId.
    /// </summary>
    private async Task MergeSignedPdfsForParallelAsync(DocumentWorkflowInstance instance)
    {
        Logger.LogInformation("[PARALLEL_MERGE] Starting merge for instance {InstanceId}", instance.Id);

        // 1. Get the original (unsigned) PDF from the workflow instance files
        var instanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id);

        if (!instanceFiles.Any())
        {
            Logger.LogWarning("[PARALLEL_MERGE] No instance files found for merge. InstanceId={InstanceId}", instance.Id);
            return;
        }

        // Get the original template/source file (first attached file)
        var originalFileRecord = await _documentFileRepository.FindAsync(instanceFiles.First().DocumentFileId);
        if (originalFileRecord == null || string.IsNullOrEmpty(originalFileRecord.Path))
        {
            Logger.LogWarning("[PARALLEL_MERGE] Original file not found or has no path. InstanceId={InstanceId}", instance.Id);
            return;
        }

        byte[] pdfBytes;
        try
        {
            pdfBytes = await _blobContainer.GetAllBytesAsync(originalFileRecord.Path);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[PARALLEL_MERGE] Error reading original PDF. Path={Path}", originalFileRecord.Path);
            throw new UserFriendlyException(L["ErrorProcessingPdf"]);
        }

        // 2. Get ALL completed assignments for this workflow, ordered by step order.
        //    Each user's signature replaces the placeholder matching their step number:
        //    Step 1 user → <<Sign01>>, Step 2 user → <<Sign02>>, etc.
        var allDoneAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.Status == nameof(DocumentAssignmentStatus.DONE)
            && x.CreationTime >= instance.StartedAt);
        allDoneAssignments = allDoneAssignments.OrderBy(a => a.StepOrder).ToList();

        if (!allDoneAssignments.Any())
        {
            Logger.LogWarning("[PARALLEL_MERGE] No completed assignments found. InstanceId={InstanceId}", instance.Id);
            return;
        }
        // Batch load users
        var userIds = allDoneAssignments.Select(a => a.ReceiverUserId).Distinct().ToList();
        var users = await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id));
        var userDict = users.ToDictionary(u => u.Id);

        // Batch load electronic signatures for all involved users
        var sigQueryable = await _userSignatureRepository.GetQueryableAsync();
        var allSignatures = await AsyncExecuter.ToListAsync(
            sigQueryable.Where(s => userIds.Contains(s.IdentityUserId)
                && s.SignType == nameof(SignType.ELECTRONIC)
                && s.IsActive));
        var signatureDict = allSignatures
            .GroupBy(s => s.IdentityUserId)
            .ToDictionary(g => g.Key, g => g.First());

        // Batch load approve logs for all completed assignments
        var assignmentIds = allDoneAssignments.Select(a => a.Id).ToList();
        var allLogs = await _documentWorkflowInstanceLogsRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id
            && assignmentIds.Contains(x.DocumentAssignmentId ?? Guid.Empty)
            && x.Action == nameof(WorkflowInstanceLogAction.APPROVE));
        var logDict = allLogs
            .GroupBy(l => l.DocumentAssignmentId ?? Guid.Empty)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(l => l.CreationTime).First());

        // 3. For each completed assignment, apply their signature to the original PDF.
        //    Use the assignment's StepOrder to match the correct placeholder in the template:
        //    Step 1 user → <<Sign01>>, Step 2 user → <<Sign02>>, etc.
        foreach (var doneAssignment in allDoneAssignments)
        {
            try
            {
                // Get user info from batch-loaded dictionary
                var userId = doneAssignment.ReceiverUserId;
                if (!userDict.TryGetValue(userId, out var user))
                {
                    Logger.LogWarning("[PARALLEL_MERGE] User {UserId} not found, skipping", userId);
                    continue;
                }
                var fullName = $"{user.Surname} {user.Name}".Trim();

                // Get user's electronic signature from batch-loaded dictionary
                if (!signatureDict.TryGetValue(userId, out var signature)
                    || string.IsNullOrWhiteSpace(signature.SignatureImage))
                {
                    Logger.LogWarning("[PARALLEL_MERGE] Skipping merge for user {UserId} - no signature found", userId);
                    continue;
                }

                // Resolve signature image bytes
                byte[] signatureImageBytes;
                try
                {
                    signatureImageBytes = await ResolveSignatureImageBytesAsync(signature.SignatureImage);
                }
                catch
                {
                    Logger.LogWarning("[PARALLEL_MERGE] Cannot resolve signature image for user {UserId}, skipping", userId);
                    continue;
                }

                // Get the note from batch-loaded logs dictionary
                logDict.TryGetValue(doneAssignment.Id, out var log);
                var noteContent = log?.Note;

                // Apply this step's placeholders to the PDF using step order
                pdfBytes = ReplacePdfPlaceholders(
                    pdfBytes,
                    doneAssignment.StepOrder,
                    signatureImageBytes,
                    fullName,
                    noteContent ?? "");

                Logger.LogInformation("[PARALLEL_MERGE] Applied signature for step {StepOrder}, user {UserId}", 
                    doneAssignment.StepOrder, userId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[PARALLEL_MERGE] Error applying signature for assignment {AssignmentId}", doneAssignment.Id);
                // Continue with other assignments - don't fail the whole merge
            }
        }

        // 4. Upload merged PDF to blob storage
        var mergedBlobPath = $"{WorkflowConstants.BlobPathElectronicSigned}parallel-merged-{Guid.NewGuid()}.pdf";
        await _blobContainer.SaveAsync(mergedBlobPath, pdfBytes);

        // 5. Create a new DocumentFile record for the merged result
        var hashString = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(pdfBytes));

        var mergedFile = new DocumentFile(
            GuidGenerator.Create(),
            instance.DocumentId,
            $"parallel-signed-{Clock.Now:yyyyMMddHHmmss}.pdf",
            true, // IsSigned = true
            Clock.Now, // ISSUE-08 FIX
            mergedBlobPath,
            hashString
        );
        mergedFile.TenantId = CurrentTenant.Id;
        await _documentFileRepository.InsertAsync(mergedFile);

        // 6. Update all completed assignments to reference the merged file
        foreach (var doneAssignment in allDoneAssignments)
        {
            doneAssignment.DocumentFileResultId = mergedFile.Id;
            await _documentAssignmentRepository.UpdateAsync(doneAssignment);
        }

        // 7. Attach merged file to workflow instance files
        var mergedInstanceFile = new DocumentWorkflowInstanceFile(
            GuidGenerator.Create(),
            instance.Id,
            mergedFile.Id
        );
        mergedInstanceFile.TenantId = CurrentTenant.Id;
        await _documentWorkflowInstanceFileRepository.InsertAsync(mergedInstanceFile);

        Logger.LogInformation("[PARALLEL_MERGE] Merge completed. MergedFileId={FileId}, BlobPath={BlobPath}", 
            mergedFile.Id, mergedBlobPath);
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
