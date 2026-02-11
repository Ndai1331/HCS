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

        var nowTime = DateTime.Now;

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
            "Initiator",
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

        // If action is APPROVE and signing method is ELECTRONIC, apply electronic signature BEFORE approve
        // (so the signed file gets copied to the next step correctly)
        if (input.Action.ToUpper() == nameof(WorkflowInstanceLogAction.APPROVE) && input.SigningMethodId.HasValue)
        {
            var signingMethod = await _masterDataRepository.FindAsync(input.SigningMethodId.Value);
            if (signingMethod != null && signingMethod.Code == nameof(SignType.ELECTRONIC))
            {
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
        await _documentAssignmentRepository.UpdateAsync(assignment, autoSave: true);

        // 2. Determine sign mode (SEQUENTIAL or PARALLEL) from the workflow template
        var template = await _workflowTemplateRepository.GetAsync(instance.WorkflowTemplateId);
        var isParallel = template.SignMode == nameof(SignMode.PARALLEL);

        // 3. Get current step info (needed for logging)
        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);

        // 4. Check remaining PENDING assignments
        //    For SEQUENTIAL: checks current step only (IsCurrent filter)
        //    For PARALLEL: checks ALL steps (all were created as IsCurrent=true at submit)
        var remainingPending = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.IsCurrent
            && x.Status == nameof(DocumentAssignmentStatus.PENDING));

        if (remainingPending.Any())
        {
            // Other users still need to process - just log and return
            await _documentWorkflowInstanceLogsManager.CreateAsync(
                instance.Id, assignment.Id, CurrentUser.Id,
                nameof(WorkflowInstanceLogAction.APPROVE),
                currentStep.Type,
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS), note);

            await UpdateDocumentStatusAsync(instance.DocumentId, DocumentStatusCode.DANG_XU_LY);
            return;
        }

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
                instance.StartedAt = now;
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
                null, //documentId
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
    ///    - &lt;&lt;Sign{stepOrder:D2}&gt;&gt; → Signature image
    ///    - &lt;&lt;FullName{stepOrder:D2}&gt;&gt; → User's full name
    ///    - &lt;&lt;NoteContent{stepOrder:D2}&gt;&gt; → Note/signing content
    /// 5. Upload signed PDF to Minio as a new file
    /// 6. Create new DocumentFile record (IsSigned = true)
    /// 7. Update assignment's DocumentFileResultId to reference the signed file
    /// 
    /// For PARALLEL signing: each user would sign on their own copy, then a merge step
    /// would combine all signatures. This can be implemented by creating per-user copies
    /// and a final merge operation using PDFsharp to overlay all signatures onto one document.
    /// </summary>
    private async Task ApplyElectronicSignatureAsync(
        DocumentAssignment assignment,
        DocumentWorkflowInstance instance,
        string? noteContent)
    {
        var currentUserId = CurrentUser.Id!.Value;

        // ==================== STEP 1: Validate user's electronic signature ====================
        Logger.LogInformation("[ELECTRONIC_SIGN] Starting electronic signing for user {UserId}, assignment {AssignmentId}, step {StepOrder}",
            currentUserId, assignment.Id, assignment.StepOrder);

        UserSignature? signature;
        try
        {
            var userSignatures = await _userSignatureRepository.GetListAsync(
                signType: nameof(SignType.ELECTRONIC),
                isActive: true);
            signature = userSignatures.FirstOrDefault(s => s.IdentityUserId == currentUserId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "[ELECTRONIC_SIGN] Error querying user signatures for user {UserId}", currentUserId);
            throw new UserFriendlyException(L["ElectronicSigningFailed", ex.Message]);
        }

        if (signature == null)
        {
            throw new UserFriendlyException(L["UserHasNoElectronicSignature"]);
        }

        // Check if signature is activated
        if (!signature.IsActive)
        {
            throw new UserFriendlyException(L["SignatureNotActivated"]);
        }

        // Check signature image is configured
        if (string.IsNullOrWhiteSpace(signature.SignatureImage))
        {
            throw new UserFriendlyException(L["SignatureImageNotConfigured"]);
        }

        // Check validity period
        var now = DateTime.Now;
        if (signature.ValidFrom.HasValue && signature.ValidFrom.Value > now)
        {
            throw new UserFriendlyException(L["SignatureNotYetValid"]);
        }
        if (signature.ValidTo.HasValue && signature.ValidTo.Value < now)
        {
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
            throw new UserFriendlyException(L["NoFileToSign"]);
        }

        DocumentFile sourceFile;
        byte[] pdfBytes;
        try
        {
            sourceFile = await _documentFileRepository.GetAsync(assignment.DocumentFileResultId.Value);
            if (string.IsNullOrEmpty(sourceFile.Path))
            {
                throw new UserFriendlyException(L["NoFileToSign"]);
            }
            pdfBytes = await _blobContainer.GetAllBytesAsync(sourceFile.Path);
        }
        catch (UserFriendlyException)
        {
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
            newBlobPath = $"electronic-signed/{Guid.NewGuid()}{extension}";
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
                    if (index < 0) continue;

                    // Get the letters that form this placeholder
                    var placeholderLetters = letters.Skip(index).Take(tag.Length).ToList();
                    if (placeholderLetters.Count < tag.Length) continue;

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

                    Logger.LogDebug("[PDF_REPLACE] Found placeholder '{Tag}' on page {Page} at ({X},{YBottom})-({MaxX},{YTop})",
                        tag, p + 1, minX, minY, maxX, maxY);
                }
            }
        }

        if (!positions.Any())
        {
            Logger.LogWarning("[PDF_REPLACE] No placeholders found for step order {StepOrder} (suffix={Suffix}). Tags: {SignTag}, {NameTag}, {NoteTag}",
                stepOrder, suffix, signTag, nameTag, noteTag);
            // Return original PDF if no placeholders found - don't throw,
            // because some steps might not have all placeholders
            return pdfBytes;
        }

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

        // 2. Get ALL completed assignments for this workflow, ordered by step order
        var allDoneAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.Status == nameof(DocumentAssignmentStatus.DONE));
        allDoneAssignments = allDoneAssignments.OrderBy(a => a.StepOrder).ToList();

        if (!allDoneAssignments.Any())
        {
            Logger.LogWarning("[PARALLEL_MERGE] No completed assignments found. InstanceId={InstanceId}", instance.Id);
            return;
        }

        // 3. For each completed assignment, apply their signature to the original PDF
        foreach (var doneAssignment in allDoneAssignments)
        {
            try
            {
                // Get user info
                var userId = doneAssignment.ReceiverUserId;
                var user = await _identityUserRepository.GetAsync(userId);
                var fullName = $"{user.Surname} {user.Name}".Trim();

                // Get user's electronic signature
                var userSignatures = await _userSignatureRepository.GetListAsync(
                    signType: nameof(SignType.ELECTRONIC),
                    isActive: true);
                var signature = userSignatures.FirstOrDefault(s => s.IdentityUserId == userId);

                if (signature == null || string.IsNullOrWhiteSpace(signature.SignatureImage))
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

                // Get the note from workflow log for this assignment
                var logs = await _documentWorkflowInstanceLogsRepository.GetListAsync(
                    x => x.DocumentWorkflowInstanceId == instance.Id
                    && x.DocumentAssignmentId == doneAssignment.Id
                    && x.Action == nameof(WorkflowInstanceLogAction.APPROVE));
                var noteContent = logs.OrderByDescending(l => l.CreationTime).FirstOrDefault()?.Note;

                // Apply this step's placeholders to the PDF
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
        var mergedBlobPath = $"electronic-signed/parallel-merged-{Guid.NewGuid()}.pdf";
        await _blobContainer.SaveAsync(mergedBlobPath, pdfBytes);

        // 5. Create a new DocumentFile record for the merged result
        var hashString = Convert.ToBase64String(
            System.Security.Cryptography.SHA256.HashData(pdfBytes));

        var mergedFile = new DocumentFile(
            GuidGenerator.Create(),
            instance.DocumentId,
            $"parallel-signed-{DateTime.Now:yyyyMMddHHmmss}.pdf",
            true, // IsSigned = true
            DateTime.Now,
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
