using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.MasterDatas;
using HC.Workflows;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Logging;
using HC.Permissions;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Users;

namespace HC.DocumentWorkflowInstances;

[Authorize(HCPermissions.DocumentAssignments.Default)]
public class DocumentSigningQueryService : HCAppService, IDocumentSigningQueryService, ITransientDependency
{
    private readonly IDocumentSigningFilterQueryBuilder _signingFilterQueryBuilder;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IRepository<Workflow, Guid> _workflowRepository;
    private readonly IRepository<WorkflowTemplate, Guid> _workflowTemplateRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IWorkflowViewAccessService _workflowViewAccessService;
    private readonly IRepository<DocumentFile, Guid> _documentFileRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<OrganizationUnit, Guid> _organizationUnitRepository;

    public DocumentSigningQueryService(
        IDocumentSigningFilterQueryBuilder signingFilterQueryBuilder,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        IRepository<Workflow, Guid> workflowRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IWorkflowViewAccessService workflowViewAccessService,
        IRepository<DocumentFile, Guid> documentFileRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<OrganizationUnit, Guid> organizationUnitRepository)
    {
        _signingFilterQueryBuilder = signingFilterQueryBuilder;
        _documentAssignmentRepository = documentAssignmentRepository;
        _masterDataRepository = masterDataRepository;
        _workflowRepository = workflowRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _workflowViewAccessService = workflowViewAccessService;
        _documentFileRepository = documentFileRepository;
        _identityUserRepository = identityUserRepository;
        _organizationUnitRepository = organizationUnitRepository;
    }

    /// <inheritdoc />
    public async Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input)
    {
        var stopwatch = Stopwatch.StartNew();

        if (!CurrentUser.Id.HasValue)
        {
            throw new Volo.Abp.Authorization.AbpAuthorizationException();
        }

        var currentUserId = CurrentUser.Id.Value;
        var now = Clock.Now;

        var filterState = await _signingFilterQueryBuilder.BuildSigningFilterStateAsync(
            currentUserId,
            input.FilterText,
            input.FilterMode,
            DocumentSigningDateFilterField.IncomingDate,
            input.FromDate,
            input.ToDate,
            input.FocusDocumentId,
            input.SubmitterUserId,
            input.SubmitterOrganizationUnitId);

        if (input.FilterMode == DocumentSigningFilterMode.Following)
        {
            return EmptyResult(filterState);
        }

        var modeFilteredQuery = filterState.ModeFilteredQuery;
        var instanceQueryable = filterState.InstanceQueryable;
        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();

        var totalCount = await AsyncExecuter.CountAsync(modeFilteredQuery);
        var myPendingAssignmentDocIdsQuery = filterState.MyPendingAssignmentDocIdsQuery;

        var latestInstanceStartedAtQuery = instanceQueryable
            .GroupBy(i => i.DocumentId)
            .Select(g => new { DocumentId = g.Key, MaxStartedAt = g.Max(x => x.StartedAt) });

        var documentSigningSortQuery =
            from d in modeFilteredQuery
            join latest in latestInstanceStartedAtQuery on d.Id equals latest.DocumentId into latestJoin
            from latest in latestJoin.DefaultIfEmpty()
            join inst in instanceQueryable on new { d.Id, latest.MaxStartedAt } equals new { Id = inst.DocumentId, MaxStartedAt = inst.StartedAt } into instJoin
            from inst in instJoin.DefaultIfEmpty()
            select new
            {
                Document = d,
                NeedsMySignature = myPendingAssignmentDocIdsQuery.Contains(d.Id),
                IsOverdueStatus = inst != null && inst.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE),
                DeadlineUrgent = inst != null
                    && inst.FinishedAt > DateTime.MinValue
                    && inst.FinishedAt <= now
                    && inst.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                DeadlineSort = inst != null && inst.FinishedAt > DateTime.MinValue ? inst.FinishedAt : DateTime.MaxValue
            };

        var pagedSortRows = await AsyncExecuter.ToListAsync(
            documentSigningSortQuery
                .OrderByDescending(x => x.NeedsMySignature)
                .ThenByDescending(x => x.IsOverdueStatus || x.DeadlineUrgent)
                .ThenBy(x => x.DeadlineSort)
                .ThenByDescending(x => x.Document.IncommingDate)
                .Skip(input.SkipCount)
                .Take(input.MaxResultCount));

        var pagedDocuments = pagedSortRows.Select(x => x.Document).ToList();

        if (pagedDocuments.Count == 0)
        {
            return new DocumentSigningPageResultDto
            {
                TotalCount = totalCount,
                Items = new List<DocumentSigningItemDto>(),
                AllCount = filterState.AllCount,
                SentToMeCount = filterState.SentToMeCount,
                SentByMeCount = filterState.SentByMeCount,
                FollowingCount = filterState.FollowingCount
            };
        }

        var pagedDocIds = pagedDocuments.Select(d => d.Id).Distinct().ToList();

        var myAssignments = await AsyncExecuter.ToListAsync(
            assignmentQueryable.Where(a =>
                pagedDocIds.Contains(a.DocumentId) &&
                a.ReceiverUserId == currentUserId &&
                a.WorkflowStepTemplateId != null));

        var latestStartedAtByDocQuery = instanceQueryable
            .Where(i => pagedDocIds.Contains(i.DocumentId))
            .GroupBy(i => i.DocumentId)
            .Select(g => new { DocumentId = g.Key, StartedAt = g.Max(x => x.StartedAt) });

        var latestInstances = await AsyncExecuter.ToListAsync(
            instanceQueryable
                .Where(i => pagedDocIds.Contains(i.DocumentId))
                .Join(
                    latestStartedAtByDocQuery,
                    i => new { i.DocumentId, i.StartedAt },
                    l => new { l.DocumentId, l.StartedAt },
                    (i, l) => i)
                .GroupBy(i => i.DocumentId)
                .Select(g => g.OrderByDescending(x => x.Id).First()));

        var latestInstancePerPagedDoc = latestInstances.ToDictionary(i => i.DocumentId, i => i);

        var masterDataIds = pagedDocuments
            .SelectMany(d => new[] { d.StatusId, (Guid?)d.TypeId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var masterDataDict = masterDataIds.Count > 0
            ? (await _masterDataRepository.GetListAsync(x => masterDataIds.Contains(x.Id)))
                .ToDictionary(m => m.Id, m => m)
            : new Dictionary<Guid, MasterData>();

        var workflowIds = latestInstances.Select(i => i.WorkflowId).Distinct().ToList();
        var workflowDict = workflowIds.Count > 0
            ? (await _workflowRepository.GetListAsync(x => workflowIds.Contains(x.Id)))
                .ToDictionary(w => w.Id, w => w)
            : new Dictionary<Guid, Workflow>();

        var stepIds = latestInstances.Select(i => i.CurrentStepId).Distinct().ToList();
        var stepDict = stepIds.Count > 0
            ? (await _workflowStepTemplateRepository.GetListAsync(x => stepIds.Contains(x.Id)))
                .ToDictionary(s => s.Id, s => s)
            : new Dictionary<Guid, WorkflowStepTemplate>();

        var templateIds = latestInstances.Select(i => i.WorkflowTemplateId).Distinct().ToList();
        var stepTemplateQueryable = await _workflowStepTemplateRepository.GetQueryableAsync();
        var allStepsForTemplates = templateIds.Count > 0
            ? await AsyncExecuter.ToListAsync(stepTemplateQueryable.Where(
                x => templateIds.Contains(x.WorkflowTemplateId) && x.IsActive))
            : new List<WorkflowStepTemplate>();

        var totalStepsDict = allStepsForTemplates
            .GroupBy(s => s.WorkflowTemplateId)
            .ToDictionary(g => g.Key, g => g.Count());

        var pageAssignments = await AsyncExecuter.ToListAsync(
            assignmentQueryable.Where(a =>
                pagedDocIds.Contains(a.DocumentId) &&
                a.WorkflowStepTemplateId != null));

        var pageWorkflowTemplateIds = latestInstancePerPagedDoc.Values
            .Select(i => i.WorkflowTemplateId)
            .Distinct()
            .ToList();

        var workflowTemplateQueryable = await _workflowTemplateRepository.GetQueryableAsync();
        var workflowTemplatesForPage = pageWorkflowTemplateIds.Count > 0
            ? await AsyncExecuter.ToListAsync(workflowTemplateQueryable.Where(x => pageWorkflowTemplateIds.Contains(x.Id)))
            : new List<WorkflowTemplate>();

        var workflowTemplateDictForPage = workflowTemplatesForPage.ToDictionary(t => t.Id, t => t);

        var committedStepIdsForPage = new HashSet<Guid>();
        foreach (var inst in latestInstancePerPagedDoc.Values)
        {
            var committedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(inst.CommittedStepTemplateIdsJson);
            if (committedIds == null)
            {
                continue;
            }

            foreach (var stepId in committedIds)
            {
                committedStepIdsForPage.Add(stepId);
            }
        }

        var committedStepIdsForPageList = committedStepIdsForPage.Distinct().ToList();
        var committedStepTemplatesForPage = committedStepIdsForPageList.Count > 0
            ? await AsyncExecuter.ToListAsync(stepTemplateQueryable.Where(x => committedStepIdsForPageList.Contains(x.Id)))
            : new List<WorkflowStepTemplate>();

        var committedStepTemplateDictForPage = committedStepTemplatesForPage.ToDictionary(s => s.Id, s => s);

        var stepsByTemplateId = allStepsForTemplates
            .GroupBy(s => s.WorkflowTemplateId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyDictionary<Guid, WorkflowStepTemplate>)g.ToDictionary(s => s.Id, s => s));

        var documentFileQueryable = await _documentFileRepository.GetQueryableAsync();
        var signedFilesForPage = pagedDocIds.Count > 0
            ? await AsyncExecuter.ToListAsync(
                documentFileQueryable.Where(f =>
                    f.DocumentId.HasValue
                    && pagedDocIds.Contains(f.DocumentId.Value)
                    && f.IsSigned))
            : new List<DocumentFile>();
        var signedFilesByDocId = signedFilesForPage
            .Where(f => f.DocumentId.HasValue)
            .GroupBy(f => f.DocumentId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DocumentFile>)g.ToList());

        var submitterUserIds = latestInstances
            .Where(i => i.CreatorId.HasValue)
            .Select(i => i.CreatorId!.Value)
            .Distinct()
            .ToList();
        var submitterNameByUserId = await GetSubmitterDisplayNamesAsync(submitterUserIds);
        var submitterOuNameByUserId = await GetSubmitterOrganizationUnitNamesAsync(submitterUserIds);

        var items = new List<DocumentSigningItemDto>();
        foreach (var doc in pagedDocuments)
        {
            latestInstancePerPagedDoc.TryGetValue(doc.Id, out var docInstance);

            var myDocAssignment = myAssignments
                .Where(a => a.DocumentId == doc.Id && a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent)
                .FirstOrDefault();

            var hasViewAccess = false;
            if (docInstance != null && currentUserId != Guid.Empty)
            {
                hasViewAccess = await _workflowViewAccessService.CanUserViewWorkflowDocumentAsync(
                    docInstance.Id, currentUserId);
            }

            string? statusName = doc.StatusId.HasValue && masterDataDict.TryGetValue(doc.StatusId.Value, out var statusMd)
                ? statusMd.Name
                : null;
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

                totalSteps = DocumentSigningQueryHelper.GetTotalStepsForDisplay(docInstance, totalStepsDict);
            }

            int? parallelSignDone = null;
            int? parallelSignTotal = null;
            if (docInstance != null
                && workflowTemplateDictForPage.TryGetValue(docInstance.WorkflowTemplateId, out var wtForParallel)
                && string.Equals(wtForParallel.SignMode, nameof(SignMode.PARALLEL), StringComparison.OrdinalIgnoreCase))
            {
                var committedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(docInstance.CommittedStepTemplateIdsJson);
                if (committedIds is { Count: > 0 })
                {
                    var signStepIds = committedIds
                        .Where(sid => committedStepTemplateDictForPage.TryGetValue(sid, out var st)
                            && string.Equals(st.Type, nameof(WorkflowStepType.SIGN), StringComparison.OrdinalIgnoreCase))
                        .ToHashSet();

                    var totalSignSteps = signStepIds.Count;
                    if (totalSignSteps > 0)
                    {
                        var doneSignSteps = pageAssignments
                            .Where(a =>
                                a.DocumentId == doc.Id
                                && a.CreationTime >= docInstance.StartedAt
                                && a.WorkflowStepTemplateId.HasValue
                                && signStepIds.Contains(a.WorkflowStepTemplateId.Value)
                                && a.Status == nameof(DocumentAssignmentStatus.DONE))
                            .Select(a => a.WorkflowStepTemplateId!.Value)
                            .Distinct()
                            .Count();

                        parallelSignDone = doneSignSteps;
                        parallelSignTotal = totalSignSteps;
                    }
                }
            }

            IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepDictForInstance =
                docInstance != null && stepsByTemplateId.TryGetValue(docInstance.WorkflowTemplateId, out var instSteps)
                    ? instSteps
                    : committedStepTemplateDictForPage;
            signedFilesByDocId.TryGetValue(doc.Id, out var signedFilesForDoc);

            var canCancelWorkflow = docInstance != null
                && docInstance.CreatorId == currentUserId
                && (docInstance.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
                    || docInstance.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE))
                && !WorkflowSigningProgressHelper.HasWorkflowSigningOccurred(
                    docInstance,
                    pageAssignments.Where(a => a.DocumentId == doc.Id).ToList(),
                    stepDictForInstance,
                    signedFilesForDoc);

            string? submitterFullName = null;
            string? submitterOrganizationUnitName = null;
            if (docInstance?.CreatorId is Guid creatorId)
            {
                submitterNameByUserId.TryGetValue(creatorId, out submitterFullName);
                submitterOuNameByUserId.TryGetValue(creatorId, out submitterOrganizationUnitName);
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
                SubmitterFullName = submitterFullName,
                SubmitterOrganizationUnitName = submitterOrganizationUnitName,
                WorkflowInstanceId = docInstance?.Id,
                WorkflowStatus = docInstance?.Status,
                CurrentStepName = currentStepName,
                CurrentStepOrder = currentStepOrder,
                TotalSteps = totalSteps,
                ParallelSignStepsCompleted = parallelSignDone,
                ParallelSignStepsTotal = parallelSignTotal,
                WorkflowStartedAt = docInstance?.StartedAt,
                WorkflowFinishedAt = docInstance != null && docInstance.FinishedAt > DateTime.MinValue
                    ? docInstance.FinishedAt
                    : null,
                WorkflowOverdueAt = docInstance?.OverdueAt,
                WorkflowGraceCancelAt = docInstance?.OverdueAt.HasValue == true
                    ? BusinessDayCalculator.GetOverdueGraceCancelAt(docInstance.OverdueAt!.Value)
                    : null,
                ExtensionCount = docInstance?.ExtensionCount ?? 0,
                TotalExtensionBusinessDays = docInstance?.TotalExtensionBusinessDays ?? 0,
                MyAssignmentStatus = myDocAssignment?.Status,
                CanAct = myDocAssignment != null && myDocAssignment.Status == nameof(DocumentAssignmentStatus.PENDING),
                HasViewAccess = hasViewAccess,
                MyAssignmentId = myDocAssignment?.Id,
                CanResubmit = docInstance != null
                    && docInstance.Status == nameof(DocumentWorkflowInstanceStatus.RETURNED)
                    && docInstance.CreatorId == currentUserId,
                CanCancelWorkflow = canCancelWorkflow
            });
        }

        stopwatch.Stop();
        Logger.LogInformation(
            "GetDocumentSigningListAsync completed in {ElapsedMs}ms for user {UserId}, mode={FilterMode}, totalCount={TotalCount}, pageSize={PageSize}",
            stopwatch.ElapsedMilliseconds,
            currentUserId,
            input.FilterMode,
            totalCount,
            input.MaxResultCount);

        return new DocumentSigningPageResultDto
        {
            TotalCount = totalCount,
            Items = items,
            AllCount = filterState.AllCount,
            SentToMeCount = filterState.SentToMeCount,
            SentByMeCount = filterState.SentByMeCount,
            FollowingCount = filterState.FollowingCount
        };
    }

    private static DocumentSigningPageResultDto EmptyResult(SigningFilterState filterState)
    {
        return new DocumentSigningPageResultDto
        {
            TotalCount = 0,
            Items = new List<DocumentSigningItemDto>(),
            AllCount = filterState.AllCount,
            SentToMeCount = filterState.SentToMeCount,
            SentByMeCount = filterState.SentByMeCount,
            FollowingCount = filterState.FollowingCount
        };
    }

    private async Task<Dictionary<Guid, string>> GetSubmitterDisplayNamesAsync(IReadOnlyList<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            userQuery
                .Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Surname, u.Name, u.UserName, u.Email }));

        return rows.ToDictionary(
            u => u.Id,
            u =>
            {
                var full = $"{u.Surname} {u.Name}".Trim();
                return string.IsNullOrWhiteSpace(full) ? u.UserName ?? u.Email ?? u.Id.ToString() : full;
            });
    }

    private async Task<Dictionary<Guid, string>> GetSubmitterOrganizationUnitNamesAsync(IReadOnlyList<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var userOuRows = await AsyncExecuter.ToListAsync(
            userQuery
                .Where(u => userIds.Contains(u.Id))
                .SelectMany(u => u.OrganizationUnits)
                .OrderBy(ou => ou.CreationTime)
                .Select(ou => new { ou.UserId, ou.OrganizationUnitId }));

        var primaryOuByUser = userOuRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().OrganizationUnitId);

        var ouIds = primaryOuByUser.Values.Distinct().ToList();
        if (ouIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ouQuery = await _organizationUnitRepository.GetQueryableAsync();
        var organizationUnits = await AsyncExecuter.ToListAsync(ouQuery.Where(x => ouIds.Contains(x.Id)));
        var ouNameById = organizationUnits.ToDictionary(x => x.Id, x => x.DisplayName ?? x.Id.ToString());

        var result = new Dictionary<Guid, string>();
        foreach (var kv in primaryOuByUser)
        {
            if (ouNameById.TryGetValue(kv.Value, out var name))
            {
                result[kv.Key] = name;
            }
        }

        return result;
    }
}
