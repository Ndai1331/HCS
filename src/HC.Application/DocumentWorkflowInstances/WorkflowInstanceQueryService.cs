using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.DocumentHistories;
using HC.Documents;
using HC.DocumentWorkflowInstanceFiles;
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
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.DocumentAssignments.Default)]
public class WorkflowInstanceQueryService : HCAppService, IWorkflowInstanceQueryService, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<WorkflowStepAssignment, Guid> _workflowStepAssignmentRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;
    private readonly IRepository<DocumentWorkflowInstanceFile, Guid> _documentWorkflowInstanceFileRepository;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IDocumentHistoryRepository _documentHistoryRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IWorkflowSubmitInfoQueryService _workflowSubmitInfoQueryService;
    private readonly IWorkflowCommittedStepsQueryService _workflowCommittedStepsQueryService;
    private readonly IWorkflowViewAccessService _workflowViewAccessService;

    public WorkflowInstanceQueryService(
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<WorkflowStepAssignment, Guid> workflowStepAssignmentRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository,
        IRepository<DocumentWorkflowInstanceFile, Guid> documentWorkflowInstanceFileRepository,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IDocumentHistoryRepository documentHistoryRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        IWorkflowSubmitInfoQueryService workflowSubmitInfoQueryService,
        IWorkflowCommittedStepsQueryService workflowCommittedStepsQueryService,
        IWorkflowViewAccessService workflowViewAccessService)
    {
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentFileRepository = documentFileRepository;
        _documentHistoryRepository = documentHistoryRepository;
        _identityUserRepository = identityUserRepository;
        _masterDataRepository = masterDataRepository;
        _workflowSubmitInfoQueryService = workflowSubmitInfoQueryService;
        _workflowCommittedStepsQueryService = workflowCommittedStepsQueryService;
        _workflowViewAccessService = workflowViewAccessService;
    }

    [Authorize(HCPermissions.Documents.SubmitForSigning)]
    public async Task<ReturnedWorkflowInfoDto> GetReturnedWorkflowInfoAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        if (instance.Status != nameof(DocumentWorkflowInstanceStatus.RETURNED))
        {
            throw new Volo.Abp.UserFriendlyException(L["WorkflowNotReturned"]);
        }

        var document = await _documentRepository.GetAsync(instance.DocumentId);
        var workflowInfo = await _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(instance.WorkflowId);

        var latestHistories = await _documentHistoryRepository.GetHistoryByDocumentIdAsync(
            instance.DocumentId, skipCount: 0, maxResultCount: 1);
        var lastHistory = latestHistories.FirstOrDefault()?.DocumentHistory;

        var instanceFiles = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == instance.Id);
        var attachedFileIds = instanceFiles.Select(f => f.DocumentFileId).ToList();
        var attachedFiles = attachedFileIds.Any()
            ? await _documentFileRepository.GetListAsync(x => attachedFileIds.Contains(x.Id))
            : new List<DocumentFile>();

        var documentFiles = await _documentFileRepository.GetListAsync(x => x.DocumentId == instance.DocumentId);

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

    public async Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId)
    {
        var instance = await _documentWorkflowInstanceRepository.GetAsync(workflowInstanceId);
        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);

        var stepIds = allSteps.Select(s => s.Id).ToList();
        var stepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
            x => x.StepId.HasValue && stepIds.Contains(x.StepId.Value) && x.IsActive);

        var docAssignments = await _documentAssignmentRepository.GetListAsync(
            x => x.DocumentId == instance.DocumentId && x.CreationTime >= instance.StartedAt);

        var allUserIds = docAssignments.Select(a => a.ReceiverUserId).Distinct().ToList();
        var users = allUserIds.Any()
            ? await _identityUserRepository.GetListAsync(x => allUserIds.Contains(x.Id))
            : new List<IdentityUser>();
        var userDict = users.ToDictionary(u => u.Id);

        var isCreator = CurrentUser.Id.HasValue && instance.CreatorId == CurrentUser.Id;
        var isCurrentStepPendingSigner = CurrentUser.Id.HasValue && docAssignments.Any(a =>
            a.WorkflowStepTemplateId == instance.CurrentStepId
            && a.Status == nameof(DocumentAssignmentStatus.PENDING)
            && a.IsCurrent
            && a.ReceiverUserId == CurrentUser.Id.Value);
        var canEditSigners = instance.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
            && (isCreator || isCurrentStepPendingSigner);
        var submitterUserId = instance.CreatorId ?? (CurrentUser.Id ?? throw new Volo.Abp.UserFriendlyException(L["NotAuthorizedForThisAction"]));
        var selectedSignerMap = WorkflowSubmissionHelper.GetStepSignerSelections(instance);

        var result = new List<WorkflowStepStatusDto>();
        var stepsNeedingSignerDetail = new List<(WorkflowStepStatusDto StepDto, WorkflowStepTemplate Step, List<WorkflowStepAssignment> TemplateAssignments)>();

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
            var thisStepDocAssignments = docAssignments.Where(a => a.WorkflowStepTemplateId == step.Id).ToList();
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
                    SigningIndex = docAssignment.Status == nameof(DocumentAssignmentStatus.DONE)
                        && WorkflowStepNavigationHelper.IsBlockingStep(step.Type)
                        && WorkflowStepNavigationHelper.TryGetSigningPlaceholderIndex(allSteps, step.Id, out var signingIndex)
                        ? signingIndex
                        : null
                });
            }

            stepDto.IsCompleted = thisStepDocAssignments.Any(a => a.Status == nameof(DocumentAssignmentStatus.DONE));

            var pendingAssignments = thisStepDocAssignments
                .Where(a => a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                .ToList();

            if (pendingAssignments.Any())
            {
                stepDto.CurrentPendingReceiverUserId = pendingAssignments.First().ReceiverUserId;
            }
            else if (selectedSignerMap.TryGetValue(step.Id, out var selectedSignerUserId))
            {
                stepDto.CurrentPendingReceiverUserId = selectedSignerUserId;
            }

            if (canEditSigners
                && !stepDto.IsCompleted
                && !WorkflowStepNavigationHelper.IsViewStep(step.Type))
            {
                stepsNeedingSignerDetail.Add((stepDto, step, thisStepTemplateAssignments));
            }

            result.Add(stepDto);
        }

        foreach (var (stepDto, step, templateAssignments) in stepsNeedingSignerDetail)
        {
            var stepDetail = await _workflowSubmitInfoQueryService.BuildWorkflowStepDetailAsync(
                step, templateAssignments, submitterUserId);
            stepDto.CanEditSigner = stepDetail.CandidateUsers.Count > 1;
            stepDto.CandidateUsers = stepDetail.CandidateUsers;
            stepDto.RoleName = stepDetail.RoleName;
        }

        return result;
    }

    public async Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId)
    {
        var instances = await _documentWorkflowInstanceRepository.GetListAsync(
            x => x.DocumentId == documentId && x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS));
        var instance = instances.FirstOrDefault();

        if (instance == null)
        {
            return null;
        }

        var currentStep = await _workflowStepTemplateRepository.GetAsync(instance.CurrentStepId);
        var allSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(instance);
        var workflow = await _workflowRepository.GetAsync(instance.WorkflowId);

        DocumentAssignmentInfoDto? myAssignment = null;
        if (CurrentUser.Id.HasValue)
        {
            var myAssignments = await _documentAssignmentRepository.GetListAsync(
                x => x.DocumentId == documentId && x.ReceiverUserId == CurrentUser.Id.Value && x.IsCurrent);
            var pending = myAssignments.FirstOrDefault(a => a.Status == nameof(DocumentAssignmentStatus.PENDING));
            if (pending != null)
            {
                var stepForAssignment = await _workflowStepTemplateRepository.FindAsync(
                    pending.WorkflowStepTemplateId ?? Guid.Empty);
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
            else if (await _workflowViewAccessService.CanUserViewWorkflowDocumentAsync(instance.Id, CurrentUser.Id.Value))
            {
                myAssignment = new DocumentAssignmentInfoDto
                {
                    AssignmentId = Guid.Empty,
                    Status = nameof(DocumentAssignmentStatus.PENDING),
                    ActionType = nameof(WorkflowStepType.VIEW),
                    StepOrder = currentStep.Order,
                    StepName = currentStep.Name,
                    IsCurrent = true,
                    CanAct = false
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

    public async Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(
        Guid workflowInstanceId)
    {
        var logs = await _documentWorkflowInstanceLogsRepository
            .GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(workflowInstanceId);

        return ObjectMapper.Map<List<DocumentWorkflowInstanceLogsWithNavigationProperties>,
            List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>>(logs);
    }

    public async Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(
        Guid workflowInstanceId)
    {
        var files = await _documentWorkflowInstanceFileRepository.GetListAsync(
            x => x.DocumentWorkflowInstanceId == workflowInstanceId);

        if (!files.Any())
        {
            return new List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>();
        }

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

    public async Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(
        Guid documentId)
    {
        var histories = await _documentHistoryRepository.GetHistoryByDocumentIdAsync(
            documentId, skipCount: 0, maxResultCount: 100);

        return ObjectMapper.Map<List<DocumentHistoryWithNavigationProperties>,
            List<DocumentHistoryWithNavigationPropertiesDto>>(histories);
    }

    public async Task<WorkflowInstanceActionBundleDto> GetActionBundleAsync(GetWorkflowInstanceActionBundleInput input)
    {
        var stopwatch = Stopwatch.StartNew();

        if (input == null)
        {
            throw new Volo.Abp.UserFriendlyException(L["ActionBundleInputRequired"]);
        }

        if (input.WorkflowInstanceId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["ActionBundleWorkflowInstanceIdRequired"]);
        }

        if (input.DocumentId == Guid.Empty)
        {
            throw new Volo.Abp.UserFriendlyException(L["ActionBundleDocumentIdRequired"]);
        }

        var bundle = new WorkflowInstanceActionBundleDto();

        var entity = await _documentWorkflowInstanceRepository.GetAsync(input.WorkflowInstanceId);
        bundle.Instance = ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(entity);

        if (bundle.Instance != null)
        {
            try
            {
                bundle.SubmitInfo = await _workflowSubmitInfoQueryService.GetWorkflowSubmitInfoAsync(bundle.Instance.WorkflowId);
                bundle.CurrentStepDetail = bundle.SubmitInfo?.Steps.FirstOrDefault(s => s.StepId == bundle.Instance.CurrentStepId);
            }
            catch (Volo.Abp.UserFriendlyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogWarning(ex, "GetActionBundleAsync: SubmitInfo fetch failed for workflowId={WorkflowId}", bundle.Instance.WorkflowId);
                throw new Volo.Abp.UserFriendlyException(L["ActionBundleSubmitInfoFailed"]);
            }
        }

        if (bundle.Instance != null
            && bundle.SubmitInfo?.SignMode != nameof(SignMode.PARALLEL)
            && bundle.Instance.CreatorId.HasValue)
        {
            var committedSteps = await _workflowCommittedStepsQueryService.LoadCommittedWorkflowStepsOrderedAsync(entity);
            var currentIndex = committedSteps.FindIndex(s => s.Id == bundle.Instance.CurrentStepId);
            if (currentIndex >= 0)
            {
                var nextBlockingIndex = WorkflowStepNavigationHelper.AdvanceThroughViewSteps(
                    entity, committedSteps, currentIndex + 1);
                if (nextBlockingIndex < committedSteps.Count
                    && WorkflowStepNavigationHelper.IsBlockingStep(committedSteps[nextBlockingIndex].Type))
                {
                    var nextStep = committedSteps[nextBlockingIndex];
                    var nextStepAssignments = await _workflowStepAssignmentRepository.GetListAsync(
                        x => x.StepId == nextStep.Id && x.IsActive);
                    bundle.NextStepDetail = await _workflowSubmitInfoQueryService.BuildWorkflowStepDetailAsync(
                        nextStep,
                        nextStepAssignments,
                        bundle.Instance.CreatorId.Value);
                }
            }
        }

        bundle.Logs = await GetWorkflowInstanceLogsAsync(input.WorkflowInstanceId);
        bundle.Files = await GetWorkflowInstanceFilesAsync(input.WorkflowInstanceId);
        bundle.DocumentHistories = await GetDocumentHistoriesByDocumentIdAsync(input.DocumentId);
        bundle.AllStepsWithStatus = await GetAllStepsWithStatusAsync(input.WorkflowInstanceId);

        var mdQuery = await _masterDataRepository.GetQueryableAsync();
        var mdTake = input.SigningMethodsMaxResultCount <= 0 ? 100 : input.SigningMethodsMaxResultCount;
        var signingMethods = await AsyncExecuter.ToListAsync(
            mdQuery
                .Where(x => x.Type == "LOAI_KY" && x.IsActive)
                .OrderBy(x => x.SortOrder)
                .Take(mdTake));
        bundle.SigningMethods = ObjectMapper.Map<List<MasterData>, List<MasterDataDto>>(signingMethods);

        stopwatch.Stop();
        Logger.LogInformation(
            "GetActionBundleAsync completed in {ElapsedMs}ms for WorkflowInstanceId={WorkflowInstanceId}, DocumentId={DocumentId}",
            stopwatch.ElapsedMilliseconds,
            input.WorkflowInstanceId,
            input.DocumentId);

        return bundle;
    }
}
