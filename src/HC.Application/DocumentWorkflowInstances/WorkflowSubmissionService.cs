using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using HC.MasterDatas;
using HC.Permissions;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Uow;
using Microsoft.Extensions.Logging;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.Documents.SubmitForSigning)]
public class WorkflowSubmissionService : HCAppService, IWorkflowSubmissionService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly DocumentWorkflowInstanceManager _documentWorkflowInstanceManager;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly DocumentAssignmentManager _documentAssignmentManager;
    private readonly DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    private readonly IRepository<DocumentWorkflowInstanceFile, Guid> _documentWorkflowInstanceFileRepository;
    private readonly DocumentManager _documentManager;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly DocumentHistoryManager _documentHistoryManager;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowSigningExecutionService _workflowSigningExecutionService;
    private readonly IWorkflowSlaService _workflowSlaService;
    private readonly IWorkflowDocumentFileService _workflowDocumentFileService;
    private readonly IWorkflowNotificationService _workflowNotificationService;

    public WorkflowSubmissionService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        DocumentWorkflowInstanceManager documentWorkflowInstanceManager,
        IRepository<Document, Guid> documentRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        DocumentAssignmentManager documentAssignmentManager,
        DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager,
        IRepository<DocumentWorkflowInstanceFile, Guid> documentWorkflowInstanceFileRepository,
        DocumentManager documentManager,
        IRepository<DocumentFile, Guid> documentFileRepository,
        DocumentHistoryManager documentHistoryManager,
        IRepository<MasterData, Guid> masterDataRepository,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowSigningExecutionService workflowSigningExecutionService,
        IWorkflowSlaService workflowSlaService,
        IWorkflowDocumentFileService workflowDocumentFileService,
        IWorkflowNotificationService workflowNotificationService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentWorkflowInstanceManager = documentWorkflowInstanceManager;
        _documentRepository = documentRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentManager = documentManager;
        _documentFileRepository = documentFileRepository;
        _documentHistoryManager = documentHistoryManager;
        _masterDataRepository = masterDataRepository;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowSigningExecutionService = workflowSigningExecutionService;
        _workflowSlaService = workflowSlaService;
        _workflowDocumentFileService = workflowDocumentFileService;
        _workflowNotificationService = workflowNotificationService;
    }

    [UnitOfWork]
    public async Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
    {
        if (input.WorkflowId == default)
        {
            throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);
        }

        var workflowInfo = await _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(input.WorkflowId);

        if (!workflowInfo.Steps.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["NoWorkflowStepsFound"]);
        }

        Guid documentId;
        Guid? templateDocumentFileId = null;
        Document? createdDocument = null;
        Guid? signingFilePreferenceAfterDuplicate = null;

        if (input.UseWorkflowTemplateFile)
        {
            var workflowTemplatePath = !string.IsNullOrWhiteSpace(workflowInfo.WordTemplatePath)
                ? workflowInfo.WordTemplatePath
                : workflowInfo.PdfTemplatePath;

            if (!workflowInfo.HasTemplateFile || string.IsNullOrWhiteSpace(workflowTemplatePath))
            {
                throw new Volo.Abp.UserFriendlyException(L["WorkflowTemplateHasNoFile"]);
            }

            var defaultTypeId = await GetDefaultMasterDataIdAsync(MasterDataType.DocumentType);
            var defaultUrgencyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.UrgencyLevel);
            var defaultSecrecyLevelId = await GetDefaultMasterDataIdAsync(MasterDataType.SecrecyLevel);

            var now = Clock.Now;
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
                sourceType: DocumentSourceType.Workflow);

            var templateFileName = Path.GetFileName(workflowTemplatePath);
            var documentFile = new DocumentFile(
                GuidGenerator.Create(),
                createdDocument.Id,
                templateFileName,
                false,
                now,
                workflowTemplatePath,
                null);
            documentFile.TenantId = CurrentTenant.Id;
            await _documentFileRepository.InsertAsync(documentFile, autoSave: true);

            documentId = createdDocument.Id;
            templateDocumentFileId = documentFile.Id;
        }
        else
        {
            if (!input.DocumentId.HasValue || input.DocumentId.Value == default)
            {
                throw new Volo.Abp.UserFriendlyException(L["The {0} field is required.", L["Document"]]);
            }

            var sourceDocumentId = input.DocumentId.Value;
            var sourceDoc = await _documentRepository.GetAsync(sourceDocumentId);

            if (sourceDoc.SourceType == DocumentSourceType.Workflow)
            {
                documentId = sourceDocumentId;
            }
            else
            {
                createdDocument = await DuplicateDocumentForWorkflowSubmitAsync(sourceDoc, input.WorkflowId);
                documentId = createdDocument.Id;
                var oldToNewFileMap = await _workflowDocumentFileService.DuplicateDocumentFilesForWorkflowAsync(
                    sourceDocumentId, documentId);
                if (input.DocumentFileId.HasValue &&
                    oldToNewFileMap.TryGetValue(input.DocumentFileId.Value, out var mappedFileId))
                {
                    signingFilePreferenceAfterDuplicate = mappedFileId;
                }
            }
        }

        if (createdDocument?.ParentDocumentId is Guid parentIdForSubmit)
        {
            await SyncParentDocumentOnWorkflowSubmitAsync(parentIdForSubmit, input.WorkflowId);
        }

        var existingInstances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => x.DocumentId == documentId && x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS));
        if (existingInstances.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["DocumentAlreadyHasActiveWorkflow"]);
        }

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

            if (oldAssignments.Any())
            {
                foreach (var oldAssignment in oldAssignments)
                {
                    oldAssignment.IsCurrent = false;
                }

                await _documentAssignmentRepository.UpdateManyAsync(oldAssignments);
                Logger.LogInformation(
                    "[RE_SUBMIT] Cleaned up {Count} old assignments for document {DocumentId} before re-submit.",
                    oldAssignments.Count, documentId);
            }
        }

        var allStepsOrdered = workflowInfo.Steps.OrderBy(s => s.Order).ToList();
        var isParallel = workflowInfo.SignMode == nameof(SignMode.PARALLEL);
        var firstBlockingStep = WorkflowStepNavigationHelper.GetFirstBlockingStepDetail(allStepsOrdered);

        if (firstBlockingStep != null && !firstBlockingStep.CandidateUsers.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        if (isParallel)
        {
            foreach (var step in WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered))
            {
                if (!step.CandidateUsers.Any())
                {
                    throw new Volo.Abp.UserFriendlyException(L["AllStepsMustHaveAssignedUsers"]);
                }
            }
        }

        var nowTime = Clock.Now;
        var blockingStepsForSla = WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered);
        var slaAnchorStep = firstBlockingStep ?? allStepsOrdered.First();

        var finishedAt = _workflowSlaService.CalculateInitialDeadline(
            nowTime,
            isParallel,
            blockingStepsForSla.Select(s => s.SLADays),
            slaAnchorStep.SLADays);

        var initialStepId = firstBlockingStep?.StepId ?? allStepsOrdered.Last().StepId;
        var initialStatus = firstBlockingStep == null
            ? nameof(DocumentWorkflowInstanceStatus.COMPLETED)
            : nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);

        var instance = await _documentWorkflowInstanceManager.CreateAsync(
            documentId,
            workflowInfo.WorkflowId,
            workflowInfo.WorkflowTemplateId,
            initialStepId,
            initialStatus,
            nowTime,
            firstBlockingStep == null ? nowTime : finishedAt);

        instance.CommittedStepTemplateIdsJson = WorkflowSubmissionHelper.SerializeCommittedStepTemplateIds(
            allStepsOrdered.Select(s => s.StepId).ToList());
        WorkflowSubmissionHelper.SetStepSignerSelections(instance, input.StepSignerSelections);
        WorkflowSubmissionHelper.ClearUnlockedViewSteps(instance);
        WorkflowStepNavigationHelper.AdvanceThroughViewSteps(instance, allStepsOrdered, 0);

        if (firstBlockingStep != null)
        {
            instance.CurrentStepId = firstBlockingStep.StepId;
            instance.FinishedAt = finishedAt;
        }
        else
        {
            instance.FinishedAt = nowTime;
        }

        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        Guid? signingFileId = templateDocumentFileId;
        if (signingFileId == null)
        {
            var filesQueryBase = (await _documentFileRepository.GetQueryableAsync())
                .Where(x => x.DocumentId == documentId);
            if (signingFilePreferenceAfterDuplicate.HasValue)
            {
                var prefQuery = filesQueryBase.Where(f => f.Id == signingFilePreferenceAfterDuplicate.Value);
                if (await AsyncExecuter.AnyAsync(prefQuery))
                {
                    signingFileId = signingFilePreferenceAfterDuplicate;
                }
            }

            if (signingFileId == null && input.DocumentFileId.HasValue)
            {
                var docFileQuery = filesQueryBase.Where(f => f.Id == input.DocumentFileId.Value);
                if (await AsyncExecuter.AnyAsync(docFileQuery))
                {
                    signingFileId = input.DocumentFileId;
                }
            }

            if (signingFileId == null)
            {
                var firstQuery = filesQueryBase.OrderBy(f => f.UploadedAt);
                var first = await AsyncExecuter.FirstOrDefaultAsync(firstQuery);
                signingFileId = first?.Id ?? input.DocumentFileId;
            }
        }

        signingFileId = await _workflowSigningExecutionService.PrepareSubmissionPlaceholdersAsync(
            signingFileId,
            documentId,
            input.SigningContent);

        var stepsToAssign = isParallel
            ? WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered).ToList()
            : firstBlockingStep != null
                ? new List<WorkflowStepDetailDto> { firstBlockingStep }
                : new List<WorkflowStepDetailDto>();
        var allNotifyUserIds = new List<Guid>();
        var firstAssignStep = stepsToAssign.FirstOrDefault();

        foreach (var step in stepsToAssign)
        {
            Guid? stepFileId = signingFileId;
            if (isParallel && firstAssignStep != null && step.Order > firstAssignStep.Order)
            {
                stepFileId = await _workflowDocumentFileService.CopyDocumentFileForNextStepAsync(signingFileId, documentId);
                if (!stepFileId.HasValue)
                {
                    Logger.LogError(
                        "[SUBMIT] CopyDocumentFileForNextStepAsync returned null for parallel step {StepOrder}. SigningFileId={SigningFileId}, DocumentId={DocumentId}",
                        step.Order, signingFileId, documentId);
                    throw new Volo.Abp.UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
                }
            }

            var receivers = ResolveReceiversForSubmit(step, input.StepSignerSelections);
            foreach (var user in receivers)
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
                    stepFileId);

                allNotifyUserIds.Add(user.UserId);
            }
        }

        foreach (var step in stepsToAssign)
        {
            foreach (var user in ResolveReceiversForSubmit(step, input.StepSignerSelections))
            {
                await _documentHistoryManager.CreateAsync(
                    documentId,
                    CurrentUser.Id,
                    user.UserId,
                    nameof(DocumentHistoryAction.TRINH),
                    input.SigningContent);
            }
        }

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id,
            null,
            CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW),
            WorkflowConstants.RoleInitiator,
            null,
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            isParallel ? $"PARALLEL - {allStepsOrdered.Count} steps" : null);

        if (input.AttachedFileIds != null && input.AttachedFileIds.Any())
        {
            foreach (var fileId in input.AttachedFileIds)
            {
                var instanceFile = new DocumentWorkflowInstanceFile(
                    GuidGenerator.Create(),
                    instance.Id,
                    fileId);
                instanceFile.TenantId = CurrentTenant.Id;
                await _documentWorkflowInstanceFileRepository.InsertAsync(instanceFile);
            }
        }

        if (signingFileId.HasValue)
        {
            var mainSigningInstanceFile = new DocumentWorkflowInstanceFile(
                GuidGenerator.Create(),
                instance.Id,
                signingFileId.Value);
            mainSigningInstanceFile.TenantId = CurrentTenant.Id;
            await _documentWorkflowInstanceFileRepository.InsertAsync(mainSigningInstanceFile);
        }

        var doc = createdDocument ?? await _documentRepository.GetAsync(documentId);
        var distinctNotifyUserIds = allNotifyUserIds.Distinct().ToList();
        if (distinctNotifyUserIds.Any())
        {
            await _workflowNotificationService.SendWorkflowNotificationAsync(
                doc,
                distinctNotifyUserIds,
                "WorkflowAssigned",
                $"WorkflowAssignedMessage|{doc.StorageNumber}|{doc.Title}|{workflowInfo.WorkflowName}|{firstBlockingStep?.Name ?? slaAnchorStep.Name}");
        }

        if (firstBlockingStep == null)
        {
            await _workflowNotificationService.UpdateDocumentStatusAsync(documentId, DocumentStatusCode.HT);
        }

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(instance);
    }

    [UnitOfWork]
    public async Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input)
    {
        var returnedInstance = await _documentWorkflowInstanceRepository.GetAsync(input.ReturnedWorkflowInstanceId);
        if (returnedInstance.Status != nameof(DocumentWorkflowInstanceStatus.RETURNED))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotReturned"]);
        }

        if (returnedInstance.CreatorId != CurrentUser.Id!.Value)
        {
            throw new Volo.Abp.UserFriendlyException(L["OnlyInitiatorCanResubmit"]);
        }

        var originalReturnedDocumentId = returnedInstance.DocumentId;
        var documentId = originalReturnedDocumentId;
        Guid? newSigningFileId = null;

        if (input.UseWorkflowTemplateFile)
        {
            var workflowInfo = await _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(returnedInstance.WorkflowId);

            var workflowTemplatePath = !string.IsNullOrWhiteSpace(workflowInfo.WordTemplatePath)
                ? workflowInfo.WordTemplatePath
                : workflowInfo.PdfTemplatePath;

            if (!workflowInfo.HasTemplateFile || string.IsNullOrWhiteSpace(workflowTemplatePath))
            {
                throw new Volo.Abp.UserFriendlyException(L["WorkflowTemplateHasNoFile"]);
            }

            var existingTemplateQuery = (await _documentFileRepository.GetQueryableAsync())
                .Where(f => f.DocumentId == documentId && f.Path == workflowTemplatePath && !f.IsSigned)
                .OrderByDescending(f => f.UploadedAt);
            var existingTemplateFile = await AsyncExecuter.FirstOrDefaultAsync(existingTemplateQuery);

            if (existingTemplateFile != null)
            {
                newSigningFileId = existingTemplateFile.Id;
            }
            else
            {
                var templateFileName = Path.GetFileName(workflowTemplatePath);
                var documentFile = new DocumentFile(
                    GuidGenerator.Create(),
                    documentId,
                    templateFileName,
                    false,
                    Clock.Now,
                    workflowTemplatePath,
                    null);
                documentFile.TenantId = CurrentTenant.Id;
                await _documentFileRepository.InsertAsync(documentFile);
                newSigningFileId = documentFile.Id;
            }
        }
        else if (input.DocumentFileId.HasValue)
        {
            newSigningFileId = input.DocumentFileId.Value;
        }

        if (input.NewDocumentId.HasValue && input.NewDocumentId.Value != documentId)
        {
            await _workflowNotificationService.UpdateDocumentStatusAsync(
                originalReturnedDocumentId, DocumentStatusCode.DA_GUI);

            var replacementDocument = await PrepareWorkflowDocumentForResubmitAsync(
                input.NewDocumentId.Value,
                returnedInstance.WorkflowId);

            documentId = replacementDocument.DocumentId;
            returnedInstance.DocumentId = documentId;
            newSigningFileId = replacementDocument.SigningFileId ?? newSigningFileId;
        }

        var oldAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == originalReturnedDocumentId
            && (x.Status == nameof(DocumentAssignmentStatus.REJECTED)
                || x.Status == nameof(DocumentAssignmentStatus.REVOKE)));

        if (oldAssignments.Any())
        {
            foreach (var oldAssignment in oldAssignments)
            {
                oldAssignment.IsCurrent = false;
            }

            await _documentAssignmentRepository.UpdateManyAsync(oldAssignments);
        }

        var submitInfo = await _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(returnedInstance.WorkflowId);
        var allStepsOrdered = submitInfo.Steps.OrderBy(s => s.Order).ToList();
        var isParallel = submitInfo.SignMode == nameof(SignMode.PARALLEL);
        var firstBlockingStep = WorkflowStepNavigationHelper.GetFirstBlockingStepDetail(allStepsOrdered);

        if (firstBlockingStep != null && !firstBlockingStep.CandidateUsers.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["FirstStepMustHaveAssignedUsers"]);
        }

        if (isParallel)
        {
            foreach (var step in WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered))
            {
                if (!step.CandidateUsers.Any())
                {
                    throw new Volo.Abp.UserFriendlyException(L["AllStepsMustHaveAssignedUsers"]);
                }
            }
        }

        var nowTime = Clock.Now;
        var blockingStepsForSla = WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered);
        var slaAnchorStep = firstBlockingStep ?? allStepsOrdered.First();

        var finishedAt = _workflowSlaService.CalculateInitialDeadline(
            nowTime,
            isParallel,
            blockingStepsForSla.Select(s => s.SLADays),
            slaAnchorStep.SLADays);

        returnedInstance.Status = firstBlockingStep == null
            ? nameof(DocumentWorkflowInstanceStatus.COMPLETED)
            : nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
        returnedInstance.WorkflowTemplateId = submitInfo.WorkflowTemplateId;
        returnedInstance.CommittedStepTemplateIdsJson = WorkflowSubmissionHelper.SerializeCommittedStepTemplateIds(
            allStepsOrdered.Select(s => s.StepId).ToList());
        WorkflowSubmissionHelper.SetStepSignerSelections(returnedInstance, input.StepSignerSelections);
        WorkflowSubmissionHelper.ClearUnlockedViewSteps(returnedInstance);
        WorkflowStepNavigationHelper.AdvanceThroughViewSteps(returnedInstance, allStepsOrdered, 0);
        returnedInstance.CurrentStepId = firstBlockingStep?.StepId ?? allStepsOrdered.Last().StepId;
        returnedInstance.StartedAt = nowTime;
        returnedInstance.FinishedAt = firstBlockingStep == null ? nowTime : finishedAt;
        returnedInstance.OverdueAt = null;
        returnedInstance.ExtensionCount = 0;
        returnedInstance.TotalExtensionBusinessDays = 0;
        await _documentWorkflowInstanceRepository.UpdateAsync(returnedInstance);

        Guid? signingFileId = newSigningFileId;
        if (signingFileId == null)
        {
            var latestFileQuery = (await _documentFileRepository.GetQueryableAsync())
                .Where(x => x.DocumentId == documentId)
                .OrderByDescending(f => f.UploadedAt);
            var latest = await AsyncExecuter.FirstOrDefaultAsync(latestFileQuery);
            signingFileId = latest?.Id;
        }

        signingFileId = await _workflowSigningExecutionService.PrepareSubmissionPlaceholdersAsync(
            signingFileId,
            documentId,
            input.SigningContent);

        var stepsToAssign = isParallel
            ? WorkflowStepNavigationHelper.GetBlockingStepDetails(allStepsOrdered).ToList()
            : firstBlockingStep != null
                ? new List<WorkflowStepDetailDto> { firstBlockingStep }
                : new List<WorkflowStepDetailDto>();
        var allNotifyUserIds = new List<Guid>();
        var firstAssignStep = stepsToAssign.FirstOrDefault();

        foreach (var step in stepsToAssign)
        {
            Guid? stepFileId = signingFileId;
            if (isParallel && firstAssignStep != null && step.Order > firstAssignStep.Order)
            {
                stepFileId = await _workflowDocumentFileService.CopyDocumentFileForNextStepAsync(signingFileId, documentId);
                if (!stepFileId.HasValue)
                {
                    Logger.LogError(
                        "[RE_SUBMIT] CopyDocumentFileForNextStepAsync returned null for parallel step {StepOrder}. SigningFileId={SigningFileId}, DocumentId={DocumentId}",
                        step.Order, signingFileId, documentId);
                    throw new Volo.Abp.UserFriendlyException(L["ErrorCopyingFileForNextStep"]);
                }
            }

            var receivers = ResolveReceiversForSubmit(step, input.StepSignerSelections);
            foreach (var user in receivers)
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
                    stepFileId);
                allNotifyUserIds.Add(user.UserId);
            }
        }

        foreach (var step in stepsToAssign)
        {
            foreach (var user in ResolveReceiversForSubmit(step, input.StepSignerSelections))
            {
                await _documentHistoryManager.CreateAsync(
                    documentId,
                    CurrentUser.Id,
                    user.UserId,
                    nameof(DocumentHistoryAction.TRINH),
                    input.SigningContent);
            }
        }

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            returnedInstance.Id,
            null,
            CurrentUser.Id,
            nameof(WorkflowInstanceLogAction.SUBMIT_WORKFLOW),
            WorkflowConstants.RoleInitiator,
            nameof(DocumentWorkflowInstanceStatus.RETURNED),
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            "RE_SUBMIT");

        if (input.AttachedFileIds != null && input.AttachedFileIds.Any())
        {
            foreach (var fileId in input.AttachedFileIds)
            {
                var instanceFile = new DocumentWorkflowInstanceFile(
                    GuidGenerator.Create(),
                    returnedInstance.Id,
                    fileId);
                instanceFile.TenantId = CurrentTenant.Id;
                await _documentWorkflowInstanceFileRepository.InsertAsync(instanceFile);
            }
        }

        if (signingFileId.HasValue)
        {
            var mainSigningInstanceFile = new DocumentWorkflowInstanceFile(
                GuidGenerator.Create(),
                returnedInstance.Id,
                signingFileId.Value);
            mainSigningInstanceFile.TenantId = CurrentTenant.Id;
            await _documentWorkflowInstanceFileRepository.InsertAsync(mainSigningInstanceFile);
        }

        if (input.DeleteFileIds != null && input.DeleteFileIds.Any())
        {
            foreach (var fileId in input.DeleteFileIds)
            {
                var referencingInstanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
                    x => x.DocumentFileId == fileId);
                foreach (var refFile in referencingInstanceFiles)
                {
                    await _documentWorkflowInstanceFileRepository.DeleteAsync(refFile);
                }

                var file = await _documentFileRepository.FindAsync(fileId);
                if (file != null)
                {
                    await _documentFileRepository.DeleteAsync(file);
                }
            }
        }

        var doc = await _documentRepository.GetAsync(documentId);
        var distinctNotifyUserIds = allNotifyUserIds.Distinct().ToList();
        if (distinctNotifyUserIds.Any())
        {
            await _workflowNotificationService.SendWorkflowNotificationAsync(
                doc,
                distinctNotifyUserIds,
                "WorkflowResubmitted",
                $"WorkflowResubmittedMessage|{doc.StorageNumber}|{doc.Title}|{submitInfo.WorkflowName}|{firstBlockingStep?.Name ?? slaAnchorStep.Name}");
        }

        await _workflowNotificationService.UpdateDocumentStatusAsync(
            documentId,
            firstBlockingStep == null ? DocumentStatusCode.HT : DocumentStatusCode.DANG_XU_LY);

        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(returnedInstance);
    }

    private List<WorkflowStepUserDto> ResolveReceiversForSubmit(
        WorkflowStepDetailDto step,
        IReadOnlyList<WorkflowStepSignerSelectionDto>? selections)
    {
        if (WorkflowStepNavigationHelper.IsViewStep(step.Type))
        {
            return new List<WorkflowStepUserDto>();
        }

        if (!step.CandidateUsers.Any())
        {
            throw new Volo.Abp.UserFriendlyException(L["NoWorkflowAssigneeCandidatesFound"]);
        }

        if (step.CandidateUsers.Count == 1)
        {
            return step.CandidateUsers;
        }

        var selection = selections?.FirstOrDefault(s => s.StepId == step.StepId);
        if (selection == null)
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowSignerSelectionRequired"]);
        }

        var selected = step.CandidateUsers.FirstOrDefault(c => c.UserId == selection.SelectedUserId);
        if (selected == null)
        {
            throw new Volo.Abp.UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
        }

        return new List<WorkflowStepUserDto> { selected };
    }

    private async Task<Document> DuplicateDocumentForWorkflowSubmitAsync(Document source, Guid workflowId)
    {
        var now = Clock.Now;
        var storageNumber = $"WF-{now:yyyyMMddHHmmssfff}";
        if (storageNumber.Length > DocumentConsts.StorageNumberMaxLength)
        {
            storageNumber = storageNumber[..DocumentConsts.StorageNumberMaxLength];
        }

        var duplicate = await _documentManager.CreateAsync(
            source.FieldId,
            source.UnitId,
            workflowId,
            source.StatusId,
            source.TypeId,
            source.UrgencyLevelId,
            source.SecrecyLevelId,
            source.Title,
            source.CompletedTime,
            storageNumber,
            source.IncommingDate,
            source.No,
            source.CurrentStatus,
            DocumentSourceType.Workflow);

        duplicate.ParentDocumentId = source.Id;
        return await _documentRepository.UpdateAsync(duplicate);
    }

    private async Task SyncParentDocumentOnWorkflowSubmitAsync(Guid parentDocumentId, Guid workflowId)
    {
        var parent = await _documentRepository.GetAsync(parentDocumentId);
        parent.WorkflowId = workflowId;
        await _documentRepository.UpdateAsync(parent);
        await _workflowNotificationService.UpdateDocumentStatusAsync(parentDocumentId, DocumentStatusCode.DANG_XU_LY);
    }

    private async Task<(Guid DocumentId, Guid? SigningFileId)> PrepareWorkflowDocumentForResubmitAsync(
        Guid selectedDocumentId,
        Guid workflowId)
    {
        var selectedDocument = await _documentRepository.GetAsync(selectedDocumentId);
        if (selectedDocument.SourceType == DocumentSourceType.Workflow)
        {
            return (selectedDocument.Id, null);
        }

        var duplicatedDocument = await DuplicateDocumentForWorkflowSubmitAsync(selectedDocument, workflowId);
        await _workflowDocumentFileService.DuplicateDocumentFilesForWorkflowAsync(
            selectedDocument.Id, duplicatedDocument.Id);
        await SyncParentDocumentOnWorkflowSubmitAsync(selectedDocument.Id, workflowId);

        var duplicatedFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == duplicatedDocument.Id);
        var preferredSigningFileId = duplicatedFiles
            .OrderBy(f => f.UploadedAt)
            .Select(f => (Guid?)f.Id)
            .FirstOrDefault();

        return (duplicatedDocument.Id, preferredSigningFileId);
    }

    private async Task<Guid> GetDefaultMasterDataIdAsync(MasterDataType type)
    {
        var typeValue = type.GetTypeValue();
        var queryable = await _masterDataRepository.GetQueryableAsync();
        var masterData = await AsyncExecuter.FirstOrDefaultAsync(
            queryable.Where(m => m.Type == typeValue).OrderBy(m => m.SortOrder));

        if (masterData == null)
        {
            throw new Volo.Abp.UserFriendlyException(L["NoDefaultMasterDataFound"] + $" ({type})");
        }

        return masterData.Id;
    }
}
