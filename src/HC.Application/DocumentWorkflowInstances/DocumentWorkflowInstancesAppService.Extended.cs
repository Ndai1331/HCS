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
using System.Text.Json;
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
using Microsoft.Extensions.Options;
using HC.Workflows;
using Volo.Abp.Uow;

namespace HC.DocumentWorkflowInstances;

public partial class DocumentWorkflowInstancesAppService : DocumentWorkflowInstancesAppServiceBase, IDocumentWorkflowInstancesAppService
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
    private readonly IWorkflowSlaService _workflowSlaService;
    private readonly IDocumentWorkflowInstanceExtensionRepository _extensionRepository;
    private readonly WorkflowSigningOptions _workflowSigningOptions;
    private readonly IDocumentSigningQueryService _documentSigningQueryService;
    private readonly IDocumentSigningFilterQueryBuilder _signingFilterQueryBuilder;
    private readonly IWorkflowSubmissionService _workflowSubmissionService;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowDocumentFileService _workflowDocumentFileService;
    private readonly IWorkflowNotificationService _workflowNotificationService;
    private readonly IWorkflowActionService _workflowActionService;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;

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
        IWorkflowSlaService workflowSlaService,
        IDocumentWorkflowInstanceExtensionRepository extensionRepository,
        IOptions<WorkflowSigningOptions> workflowSigningOptions,
        IDocumentSigningQueryService documentSigningQueryService,
        IDocumentSigningFilterQueryBuilder signingFilterQueryBuilder,
        IWorkflowSubmissionService workflowSubmissionService,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowDocumentFileService workflowDocumentFileService,
        IWorkflowNotificationService workflowNotificationService,
        IWorkflowActionService workflowActionService,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService
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
        _workflowSlaService = workflowSlaService;
        _extensionRepository = extensionRepository;
        _workflowSigningOptions = workflowSigningOptions.Value;
        _documentSigningQueryService = documentSigningQueryService;
        _signingFilterQueryBuilder = signingFilterQueryBuilder;
        _workflowSubmissionService = workflowSubmissionService;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowDocumentFileService = workflowDocumentFileService;
        _workflowNotificationService = workflowNotificationService;
        _workflowActionService = workflowActionService;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
    }

    #region GetWorkflowSubmitInfoAsync

    /// <summary>
    /// Returns true if the document's first file is .doc or .docx.
    /// Used when submitting with "my document" to determine if SigningContent is required.
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<bool> IsDocumentSourceFileWordFormatAsync(Guid documentId)
        => _workflowSubmitInfoQueryService.IsDocumentSourceFileWordFormatAsync(documentId);

    /// <summary>
    /// Get workflow info (steps, assignments, template) for the submit modal
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId)
        => _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(workflowId);

    #endregion

    #region SubmitToWorkflowAsync

    /// <summary>
    /// Submit a document to a workflow.
    /// </summary>
    [UnitOfWork]
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
        => _workflowSubmissionService.SubmitToWorkflowAsync(input);

    #endregion

    #region ProcessWorkflowActionAsync

    /// <summary>
    /// Process a workflow action: APPROVE, RETURN, REJECT
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
        => _workflowActionService.ProcessWorkflowActionAsync(input);

    #endregion

    #region ResubmitReturnedWorkflowAsync

    /// <summary>
    /// Re-submit a workflow that was previously returned (RETURNED status).
    /// </summary>
    [UnitOfWork]
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input)
        => _workflowSubmissionService.ResubmitReturnedWorkflowAsync(input);

    #endregion

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

    #region GetAllStepsWithStatusAsync

    /// <summary>
    /// Get all workflow steps with their signing status for the action modal.
    /// Shows each step name, assigned users, and whether they have signed (with signing index).
    /// </summary>
    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);

        // Steps frozen at submit/resubmit — not the live template (may have extra steps later).
        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);

        // Get all step assignments (for step user info)
        var stepIds = allSteps.Select(s => s.Id).ToList();
        var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && stepIds.Contains(x.StepId.Value) && x.IsActive);

        // Get all document assignments for this document (current workflow pass)
        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
            && x.CreationTime >= instance.StartedAt);

        // Batch load all involved user IDs
        var allUserIds = docAssignments.Select(a => a.ReceiverUserId).Distinct().ToList();
        var users = allUserIds.Any()
            ? await _identityUserRepository.GetListAsync(x => allUserIds.Contains(x.Id))
            : new List<IdentityUser>();
        var userDict = users.ToDictionary(u => u.Id);

        var isCreator = CurrentUser.Id.HasValue && instance.CreatorId == CurrentUser.Id;
        var canEditSigners = isCreator && instance.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
        var submitterUserId = instance.CreatorId ?? CurrentUser.Id!.Value;

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

            var thisStepTemplateAssignments = stepAssignments.Where(sa => sa.StepId == step.Id).ToList();

            var thisStepDocAssignments = docAssignments
                .Where(a => a.WorkflowStepTemplateId == step.Id)
                .ToList();

            var displayedUserIds = new HashSet<Guid>();

            foreach (var docAssignment in thisStepDocAssignments.OrderByDescending(a => a.IsCurrent).ThenBy(a => a.CreationTime))
            {
                if (!displayedUserIds.Add(docAssignment.ReceiverUserId))
                {
                    continue;
                }

                userDict.TryGetValue(docAssignment.ReceiverUserId, out var user);
                var templateAssignment = thisStepTemplateAssignments.FirstOrDefault(sa =>
                    sa.DefaultUserId == docAssignment.ReceiverUserId);

                stepDto.Users.Add(new StepAssignmentUserDto
                {
                    UserId = docAssignment.ReceiverUserId,
                    FullName = user != null ? $"{user.Surname} {user.Name}".Trim() : null,
                    UserName = user?.UserName,
                    IsPrimary = templateAssignment?.IsPrimary ?? false,
                    Status = docAssignment.Status,
                    ProcessedAt = docAssignment.ProcessedAt > DateTime.MinValue ? docAssignment.ProcessedAt : null,
                    SigningIndex = docAssignment.Status == nameof(DocumentAssignmentStatus.DONE) ? step.Order : null
                });
            }

            stepDto.IsCompleted = thisStepDocAssignments.Any(a => a.Status == nameof(DocumentAssignmentStatus.DONE));

            var pendingAssignments = thisStepDocAssignments
                .Where(a => a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                .ToList();

            if (pendingAssignments.Any())
            {
                stepDto.CurrentPendingReceiverUserId = pendingAssignments.First().ReceiverUserId;

                if (canEditSigners && !stepDto.IsCompleted)
                {
                    var stepDetail = await _workflowSubmitInfoQueryService.BuildWorkflowStepDetailAsync(step, thisStepTemplateAssignments, submitterUserId);
                    stepDto.CanEditSigner = true;
                    stepDto.CandidateUsers = stepDetail.CandidateUsers;
                    stepDto.RoleName = stepDetail.RoleName;
                }
            }

            result.Add(stepDto);
        }

        return result;
    }

    /// <summary>
    /// Allows the workflow creator to change pending signers on steps that have not been completed.
    /// </summary>
    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        if (instance.CreatorId != CurrentUser.Id)
        {
            throw new UserFriendlyException(L["NotAuthorizedForThisAction"]);
        }

        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS))
        {
            throw new UserFriendlyException(L["WorkflowNotInProgress"]);
        }

        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        var stepIds = allSteps.Select(s => s.Id).ToList();
        var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && stepIds.Contains(x.StepId.Value) && x.IsActive);

        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId && x.CreationTime >= instance.StartedAt);

        var stepsStatus = await GetAllStepsWithStatusAsync(input.WorkflowInstanceId);
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
                throw new UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
            }

            if (!stepStatus.CandidateUsers.Any(c => c.UserId == selection.SelectedUserId))
            {
                throw new UserFriendlyException(L["InvalidWorkflowSignerSelection"]);
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
                throw new UserFriendlyException(L["WorkflowStepSignerNotEditable"]);
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
            var updateSignerNote = BuildUpdateSignerLogNote(
                step.Order,
                step.Name,
                FormatIdentityUserDisplayName(fromUser),
                FormatIdentityUserDisplayName(toUser));

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
        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
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

    public Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input)
        => _documentSigningQueryService.GetDocumentSigningListAsync(input);

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

            var terminalStatuses = new[]
            {
                nameof(DocumentWorkflowInstanceStatus.COMPLETED),
                nameof(DocumentWorkflowInstanceStatus.REJECTED),
                nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                nameof(DocumentWorkflowInstanceStatus.RETURNED)
            };

            result.WorkflowStatus = instance.Status;
            result.ExtensionCount = instance.ExtensionCount;
            result.TotalExtensionBusinessDays = instance.TotalExtensionBusinessDays;

            if (instance.Status == nameof(DocumentWorkflowInstanceStatus.CANCELLED))
            {
                result.IsOverdue = true;
            }
            else if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE)
                     && instance.OverdueAt.HasValue)
            {
                result.IsOverdue = true;
                result.GraceCancelAt = BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt.Value);
                result.CanExtend = Clock.Now < result.GraceCancelAt.Value
                    && await CanUserExtendWorkflowAsync(instance, currentUserId);
            }
            else if (instance.FinishedAt > DateTime.MinValue
                     && instance.FinishedAt <= Clock.Now
                     && !terminalStatuses.Contains(instance.Status))
            {
                result.IsOverdue = true;
                result.CanExtend = await CanUserExtendWorkflowAsync(instance, currentUserId)
                    && IsNearDeadlineForExtension(instance);
            }
            else if (instance.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
                     && instance.FinishedAt > DateTime.MinValue)
            {
                result.CanExtend = await CanUserExtendWorkflowAsync(instance, currentUserId)
                    && IsNearDeadlineForExtension(instance);
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

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task ExtendWorkflowAsync(ExtendWorkflowInput input)
    {
        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new UserFriendlyException(L["The {0} field is required.", "WorkflowInstanceId"]);
        }

        if (input.ExtensionBusinessDays < 1)
        {
            throw new UserFriendlyException(L["ExtensionBusinessDaysMustBePositive"]);
        }

        if (string.IsNullOrWhiteSpace(input.Reason))
        {
            throw new UserFriendlyException(L["ExtensionReasonRequired"]);
        }

        var instance = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        var currentUserId = CurrentUser.Id!.Value;

        if (!await CanUserExtendWorkflowAsync(instance, currentUserId))
        {
            throw new UserFriendlyException(L["NotAuthorizedToExtendWorkflow"]);
        }

        var allowedStatuses = new[]
        {
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            nameof(DocumentWorkflowInstanceStatus.OVERDUE)
        };

        if (!allowedStatuses.Contains(instance.Status))
        {
            throw new UserFriendlyException(L["WorkflowCannotBeExtended"]);
        }

        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            if (!instance.OverdueAt.HasValue
                || Clock.Now >= BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt.Value))
            {
                throw new UserFriendlyException(L["WorkflowOverdueGraceExpired"]);
            }
        }
        else if (!IsNearDeadlineForExtension(instance))
        {
            throw new UserFriendlyException(L["WorkflowExtensionNotNearDeadline"]);
        }

        var now = Clock.Now;
        var previousFinishedAt = instance.FinishedAt;
        var previousStatus = instance.Status;
        var newFinishedAt = _workflowSlaService.CalculateExtensionDeadline(now, previousFinishedAt, input.ExtensionBusinessDays);

        instance.FinishedAt = newFinishedAt;
        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            instance.Status = nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
            instance.OverdueAt = null;
        }

        instance.ExtensionCount++;
        instance.TotalExtensionBusinessDays += input.ExtensionBusinessDays;
        await _documentWorkflowInstanceRepository.UpdateAsync(instance);

        await _extensionRepository.InsertAsync(new DocumentWorkflowInstanceExtension(
            GuidGenerator.Create(),
            instance.Id,
            currentUserId,
            input.ExtensionBusinessDays,
            previousFinishedAt,
            newFinishedAt,
            input.Reason.Trim(),
            previousStatus,
            instance.Status));

        var extensionLogNote = BuildExtensionLogNote(
            input.Reason.Trim(),
            input.ExtensionBusinessDays,
            previousFinishedAt,
            newFinishedAt);

        await _documentWorkflowInstanceLogsManager.CreateAsync(
            instance.Id,
            null,
            currentUserId,
            nameof(WorkflowInstanceLogAction.EXTEND_WORKFLOW),
            null,
            previousStatus,
            instance.Status,
            extensionLogNote);

        var pendingSigners = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId
                 && x.WorkflowStepTemplateId == instance.CurrentStepId
                 && x.Status == nameof(DocumentAssignmentStatus.PENDING)
                 && x.IsCurrent);

        if (pendingSigners.Any())
        {
            var document = await _documentRepository.GetAsync(instance.DocumentId);
            var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);
            var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
            await _workflowNotificationService.SendWorkflowNotificationAsync(
                document,
                pendingSigners.Select(a => a.ReceiverUserId).Distinct().ToList(),
                "WorkflowExtended",
                $"WorkflowExtendedMessage|{document.StorageNumber}|{document.Title}|{workflow.Name}|{currentStep.Name}|{input.ExtensionBusinessDays}");
        }
    }

    [Authorize(HCPermissions.DocumentAssignments.Default)]
    public async Task<WorkflowExtensionSummaryDto> GetWorkflowExtensionSummaryAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        var extensions = await _extensionRepository.GetListByInstanceIdAsync(workflowInstanceId);

        var userIds = extensions.Select(e => e.ExtendedByUserId).Distinct().ToList();
        var users = userIds.Any()
            ? await _identityUserRepository.GetListAsync(x => userIds.Contains(x.Id))
            : new List<IdentityUser>();
        var userDict = users.ToDictionary(u => u.Id);

        return new WorkflowExtensionSummaryDto
        {
            ExtensionCount = instance.ExtensionCount,
            TotalExtensionBusinessDays = instance.TotalExtensionBusinessDays,
            History = extensions.Select(e =>
            {
                userDict.TryGetValue(e.ExtendedByUserId, out var user);
                return new WorkflowExtensionHistoryItemDto
                {
                    Id = e.Id,
                    CreationTime = e.CreationTime,
                    ExtendedByUserId = e.ExtendedByUserId,
                    ExtendedByUserName = user != null ? $"{user.Surname} {user.Name}".Trim() : user?.UserName,
                    ExtensionBusinessDays = e.ExtensionBusinessDays,
                    PreviousFinishedAt = e.PreviousFinishedAt,
                    NewFinishedAt = e.NewFinishedAt,
                    Reason = e.Reason
                };
            }).ToList()
        };
    }

    private async Task<bool> CanUserExtendWorkflowAsync(DocumentWorkflowInstance instance, Guid currentUserId)
    {
        if (IsWorkflowAdminUser())
        {
            return true;
        }

        return await _documentAssignmentRepository.AnyAsync(a =>
            a.DocumentId == instance.DocumentId
            && a.WorkflowStepTemplateId == instance.CurrentStepId
            && a.ReceiverUserId == currentUserId
            && a.Status == nameof(DocumentAssignmentStatus.PENDING)
            && a.IsCurrent);
    }

    private bool IsWorkflowAdminUser()
    {
        return CurrentUser.IsInRole("admin") || CurrentUser.IsInRole("ADMIN");
    }

    private bool IsNearDeadlineForExtension(DocumentWorkflowInstance instance)
    {
        if (instance.FinishedAt <= DateTime.MinValue)
        {
            return false;
        }

        if (instance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
        {
            return true;
        }

        var threshold = Clock.Now.AddHours(_workflowSigningOptions.NearDeadlineHours);
        return instance.FinishedAt <= threshold;
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
                await _workflowNotificationService.UpdateDocumentStatusAsync(instance.DocumentId, documentStatusCode.Value);
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

    #region Helper Methods

    private static string FormatIdentityUserDisplayName(IdentityUser? user)
    {
        if (user == null)
        {
            return "---";
        }

        var fullName = $"{user.Surname} {user.Name}".Trim();
        return string.IsNullOrWhiteSpace(fullName) ? user.UserName ?? "---" : fullName;
    }

    private string BuildExtensionLogNote(string reason, int extensionBusinessDays, DateTime previousFinishedAt, DateTime newFinishedAt)
    {
        var fromText = previousFinishedAt > DateTime.MinValue
            ? previousFinishedAt.ToString("dd/MM/yyyy HH:mm")
            : "---";
        var toText = newFinishedAt > DateTime.MinValue
            ? newFinishedAt.ToString("dd/MM/yyyy HH:mm")
            : "---";
        var detail = L["WorkflowLogExtensionDetail", extensionBusinessDays, fromText, toText];
        return string.IsNullOrWhiteSpace(reason) ? detail : $"{reason.Trim()}{Environment.NewLine}{detail}";
    }

    private string BuildUpdateSignerLogNote(int stepOrder, string stepName, string fromUserName, string toUserName)
    {
        return L["WorkflowLogUpdateSignerDetail", stepOrder, stepName, fromUserName, toUserName];
    }

    #endregion

    #region M3 ActionBundle

    /// <summary>
    /// Returns every piece of data the Signing modal needs on open in a single response.
    /// Calls are issued sequentially server-side (single DbContext), but the client observes one RTT.
    /// </summary>
    public virtual async Task<WorkflowInstanceActionBundleDto> GetActionBundleAsync(GetWorkflowInstanceActionBundleInput input)
    {
        if (input == null) throw new UserFriendlyException("input is required");
        if (input.WorkflowInstanceId == Guid.Empty) throw new UserFriendlyException("WorkflowInstanceId is required");
        if (input.DocumentId == Guid.Empty) throw new UserFriendlyException("DocumentId is required");

        var bundle = new WorkflowInstanceActionBundleDto();

        // 1) Instance + submit info + current step detail.
        var instance = await GetAsync(input.WorkflowInstanceId);
        bundle.Instance = instance;

        if (instance != null)
        {
            try
            {
                bundle.SubmitInfo = await GetWorkflowSubmitInfoAsync(instance.WorkflowId);
                bundle.CurrentStepDetail = bundle.SubmitInfo?.Steps.FirstOrDefault(s => s.StepId == instance.CurrentStepId);
            }
            catch (Exception ex)
            {
                // SubmitInfo is a nice-to-have; failing should not blow up the whole modal open.
                Logger.LogWarning(ex, "GetActionBundleAsync: SubmitInfo fetch failed for workflowId={workflowId}", instance.WorkflowId);
            }
        }

        // 2) Logs / files / histories / all-steps-with-status.
        bundle.Logs = await GetWorkflowInstanceLogsAsync(input.WorkflowInstanceId);
        bundle.Files = await GetWorkflowInstanceFilesAsync(input.WorkflowInstanceId);
        bundle.DocumentHistories = await GetDocumentHistoriesByDocumentIdAsync(input.DocumentId);
        bundle.AllStepsWithStatus = await GetAllStepsWithStatusAsync(input.WorkflowInstanceId);

        // 3) Signing methods (LOAI_KY master data) — small list, queried directly here
        //    to avoid coupling to MasterDatasAppService and its permission check.
        var mdQuery = await _masterDataRepository.GetQueryableAsync();
        var mdTake = input.SigningMethodsMaxResultCount <= 0 ? 100 : input.SigningMethodsMaxResultCount;
        var signingMethods = await AsyncExecuter.ToListAsync(
            mdQuery
                .Where(x => x.Type == "LOAI_KY" && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Take(mdTake));
        bundle.SigningMethods = ObjectMapper.Map<List<MasterData>, List<MasterDataDto>>(signingMethods);

        return bundle;
    }

    #endregion
}
