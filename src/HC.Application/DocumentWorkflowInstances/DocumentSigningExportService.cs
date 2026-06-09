using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.DocumentAssignments;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstances;
using HC.MasterDatas;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using MiniExcelLibs;
using Volo.Abp;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Volo.Abp.Content;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

public class DocumentSigningExportService : HCAppService, IDocumentSigningExportService, ITransientDependency
{
    private const int SigningExportMaxRows = 10_000;

    private readonly IDocumentSigningFilterQueryBuilder _signingFilterQueryBuilder;
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IDocumentHistoryRepository _documentHistoryRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly IRepository<WorkflowTemplate, Guid> _workflowTemplateRepository;
    private readonly IRepository<WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> _downloadTokenCache;

    public DocumentSigningExportService(
        IDocumentSigningFilterQueryBuilder signingFilterQueryBuilder,
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IDocumentAssignmentRepository documentAssignmentRepository,
        IDocumentHistoryRepository documentHistoryRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        IRepository<WorkflowTemplate, Guid> workflowTemplateRepository,
        IRepository<WorkflowStepTemplate, Guid> workflowStepTemplateRepository,
        IRepository<IdentityUser, Guid> identityUserRepository,
        IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _signingFilterQueryBuilder = signingFilterQueryBuilder;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentHistoryRepository = documentHistoryRepository;
        _masterDataRepository = masterDataRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _identityUserRepository = identityUserRepository;
        _downloadTokenCache = downloadTokenCache;
    }

    /// <summary>
    /// Excel export for the document signing page (all matching rows, same scope as the grid).
    /// </summary>
    [AllowAnonymous]
    public async Task<IRemoteStreamContent> GetDocumentSigningListAsExcelFileAsync(DocumentSigningExcelDownloadDto input)
    {
        var currentUserId = await ValidateAndConsumeSigningExportTokenAsync(input.DownloadToken, input.ExportAllUsers);
        var now = Clock.Now;

        var filterState = input.ExportAllUsers
            ? await _signingFilterQueryBuilder.BuildAllUsersSigningFilterStateAsync(
                input.FilterText,
                input.FilterMode,
                input.DateFilterField,
                input.FromDate,
                input.ToDate,
                focusDocumentId: null,
                input.SubmitterUserId,
                input.SubmitterOrganizationUnitId)
            : await _signingFilterQueryBuilder.BuildSigningFilterStateAsync(
                currentUserId,
                input.FilterText,
                input.FilterMode,
                input.DateFilterField,
                input.FromDate,
                input.ToDate,
                focusDocumentId: null,
                input.SubmitterUserId,
                input.SubmitterOrganizationUnitId);

        if (input.FilterMode == DocumentSigningFilterMode.Following)
        {
            return await CreateSigningExcelStreamAsync(new List<Dictionary<string, object?>>(), input.ExportAllUsers);
        }

        var documentSigningSortQuery =
            from d in filterState.ModeFilteredQuery
            join latest in filterState.InstanceQueryable
                .GroupBy(i => i.DocumentId)
                .Select(g => new { DocumentId = g.Key, MaxStartedAt = g.Max(x => x.StartedAt) })
                on d.Id equals latest.DocumentId into latestJoin
            from latest in latestJoin.DefaultIfEmpty()
            join inst in filterState.InstanceQueryable
                on new { d.Id, latest.MaxStartedAt } equals new { Id = inst.DocumentId, MaxStartedAt = inst.StartedAt } into instJoin
            from inst in instJoin.DefaultIfEmpty()
            select new
            {
                Document = d,
                NeedsMySignature = filterState.MyPendingAssignmentDocIdsQuery.Contains(d.Id),
                IsOverdueStatus = inst != null && inst.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE),
                DeadlineUrgent = inst != null
                    && inst.FinishedAt > DateTime.MinValue
                    && inst.FinishedAt <= now
                    && inst.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                DeadlineSort = inst != null && inst.FinishedAt > DateTime.MinValue ? inst.FinishedAt : DateTime.MaxValue
            };

        var totalCount = await AsyncExecuter.CountAsync(filterState.ModeFilteredQuery);
        if (totalCount > SigningExportMaxRows)
        {
            Logger.LogWarning(
                "Signing export capped at {MaxRows}. Total matching documents: {TotalCount}",
                SigningExportMaxRows, totalCount);
        }

        var sortRows = await AsyncExecuter.ToListAsync(
            documentSigningSortQuery
                .OrderByDescending(x => x.NeedsMySignature)
                .ThenByDescending(x => x.IsOverdueStatus || x.DeadlineUrgent)
                .ThenBy(x => x.DeadlineSort)
                .ThenByDescending(x => x.Document.IncommingDate)
                .Take(SigningExportMaxRows));

        var documents = sortRows.Select(x => x.Document).ToList();
        var exportRows = await BuildSigningExportRowsAsync(documents);
        return await CreateSigningExcelStreamAsync(exportRows, input.ExportAllUsers);
    }

    private async Task<IRemoteStreamContent> CreateSigningExcelStreamAsync(
        List<Dictionary<string, object?>> rows,
        bool exportAllUsers = false)
    {
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(rows);
        memoryStream.Seek(0, SeekOrigin.Begin);
        var fileName = exportAllUsers
            ? $"kyduyet_all_users_{Clock.Now:yyyyMMdd_HHmm}.xlsx"
            : $"kyduyet_{Clock.Now:yyyyMMdd_HHmm}.xlsx";
        return new RemoteStreamContent(
            memoryStream,
            fileName,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    private async Task<List<Dictionary<string, object?>>> BuildSigningExportRowsAsync(List<Document> documents)
    {
        if (documents.Count == 0)
        {
            return new List<Dictionary<string, object?>>();
        }

        var docIds = documents.Select(d => d.Id).Distinct().ToList();
        var instanceQueryable = await _documentWorkflowInstanceRepository.GetQueryableAsync();
        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();

        var latestStartedAtByDocQuery = instanceQueryable
            .Where(i => docIds.Contains(i.DocumentId))
            .GroupBy(i => i.DocumentId)
            .Select(g => new { DocumentId = g.Key, StartedAt = g.Max(x => x.StartedAt) });

        var latestInstances = await AsyncExecuter.ToListAsync(
            instanceQueryable
                .Where(i => docIds.Contains(i.DocumentId))
                .Join(
                    latestStartedAtByDocQuery,
                    i => new { i.DocumentId, i.StartedAt },
                    l => new { l.DocumentId, l.StartedAt },
                    (i, l) => i)
                .GroupBy(i => i.DocumentId)
                .Select(g => g.OrderByDescending(x => x.Id).First()));

        var instanceByDocId = latestInstances.ToDictionary(i => i.DocumentId, i => i);

        var allAssignments = await AsyncExecuter.ToListAsync(
            assignmentQueryable.Where(a =>
                docIds.Contains(a.DocumentId) && a.WorkflowStepTemplateId != null));

        var trinhAction = nameof(DocumentHistoryAction.TRINH);
        var historyQueryable = await _documentHistoryRepository.GetQueryableAsync();
        var trinhHistories = await AsyncExecuter.ToListAsync(
            historyQueryable
                .Where(h => docIds.Contains(h.DocumentId) && h.Action == trinhAction)
                .OrderByDescending(h => h.CreationTime));

        var latestTrinhByDoc = trinhHistories
            .GroupBy(h => h.DocumentId)
            .ToDictionary(g => g.Key, g => g.First().Comment);

        var masterDataIds = documents
            .Select(d => d.TypeId)
            .Distinct()
            .ToList();
        var masterDataDict = masterDataIds.Any()
            ? (await _masterDataRepository.GetListAsync(x => masterDataIds.Contains(x.Id)))
                .ToDictionary(m => m.Id, m => m.Name)
            : new Dictionary<Guid, string>();

        var templateIds = latestInstances.Select(i => i.WorkflowTemplateId).Distinct().ToList();
        var workflowTemplateDict = templateIds.Any()
            ? (await _workflowTemplateRepository.GetListAsync(x => templateIds.Contains(x.Id)))
                .ToDictionary(t => t.Id, t => t.Name)
            : new Dictionary<Guid, string>();

        var userIds = new HashSet<Guid>();
        foreach (var inst in latestInstances)
        {
            if (inst.CreatorId.HasValue)
            {
                userIds.Add(inst.CreatorId.Value);
            }
        }

        foreach (var a in allAssignments)
        {
            userIds.Add(a.ReceiverUserId);
        }

        var userDisplayNames = await GetSigningExportUserDisplayNamesAsync(userIds);
        var userOuNames = await GetSigningExportUserOrganizationUnitNamesAsync(userIds);

        var stepTemplateIds = new HashSet<Guid>();
        foreach (var inst in latestInstances)
        {
            var ids = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(inst.CommittedStepTemplateIdsJson);
            if (ids == null)
            {
                continue;
            }

            foreach (var id in ids)
            {
                stepTemplateIds.Add(id);
            }
        }

        var stepTemplates = stepTemplateIds.Any()
            ? await _workflowStepTemplateRepository.GetListAsync(x => stepTemplateIds.Contains(x.Id))
            : new List<WorkflowStepTemplate>();
        var stepTemplateDict = stepTemplates.ToDictionary(s => s.Id, s => s);

        var legacyStepsByTemplateId = templateIds.Any()
            ? (await _workflowStepTemplateRepository.GetListAsync(
                x => templateIds.Contains(x.WorkflowTemplateId) && x.IsActive))
                .GroupBy(s => s.WorkflowTemplateId)
                .ToDictionary(g => g.Key, g => g.OrderBy(s => s.Order).ToList())
            : new Dictionary<Guid, List<WorkflowStepTemplate>>();

        var rows = new List<Dictionary<string, object?>>();
        foreach (var doc in documents)
        {
            instanceByDocId.TryGetValue(doc.Id, out var instance);
            var instanceAssignments = instance == null
                ? new List<DocumentAssignment>()
                : allAssignments
                    .Where(a => a.DocumentId == doc.Id && a.CreationTime >= instance.StartedAt)
                    .ToList();

            var committedSteps = instance == null
                ? new List<WorkflowStepTemplate>()
                : ResolveCommittedStepsOrderedForExport(instance, stepTemplateDict, legacyStepsByTemplateId);

            var stepChainParts = new List<string>();
            for (var i = 0; i < committedSteps.Count; i++)
            {
                var step = committedSteps[i];
                var stepAssignees = instanceAssignments
                    .Where(a => a.WorkflowStepTemplateId == step.Id)
                    .OrderByDescending(a => a.IsCurrent)
                    .ThenBy(a => a.CreationTime)
                    .ToList();

                var signerName = "---";
                if (stepAssignees.Count > 0)
                {
                    var primary = stepAssignees.First();
                    userDisplayNames.TryGetValue(primary.ReceiverUserId, out signerName);
                }

                stepChainParts.Add($"Step {i + 1}: {signerName}");
            }

            var stepChain = string.Join(" | ", stepChainParts);
            var totalSteps = committedSteps.Count;
            var signedStepCount = committedSteps.Count(step =>
                instanceAssignments.Any(a =>
                    a.WorkflowStepTemplateId == step.Id
                    && a.Status == nameof(DocumentAssignmentStatus.DONE)));

            var lastStep = committedSteps.LastOrDefault();
            string? receiverUnit = null;
            string? finalSignerName = null;
            string? finalSignerDept = null;
            string? pendingSignerName = null;
            DateTime? lastSignTime = null;

            if (lastStep != null)
            {
                var lastStepAssignments = instanceAssignments
                    .Where(a => a.WorkflowStepTemplateId == lastStep.Id)
                    .ToList();

                var lastPending = lastStepAssignments
                    .FirstOrDefault(a => a.Status == nameof(DocumentAssignmentStatus.PENDING) && a.IsCurrent);

                if (lastPending != null)
                {
                    userDisplayNames.TryGetValue(lastPending.ReceiverUserId, out pendingSignerName);
                    userOuNames.TryGetValue(lastPending.ReceiverUserId, out receiverUnit);
                }
                else
                {
                    var lastAssignee = lastStepAssignments
                        .OrderByDescending(a => a.CreationTime)
                        .FirstOrDefault();
                    if (lastAssignee != null)
                    {
                        userDisplayNames.TryGetValue(lastAssignee.ReceiverUserId, out var name);
                        pendingSignerName = name;
                        userOuNames.TryGetValue(lastAssignee.ReceiverUserId, out receiverUnit);
                    }
                }
            }

            var doneAssignments = instanceAssignments
                .Where(a => a.Status == nameof(DocumentAssignmentStatus.DONE)
                            && a.ProcessedAt > DateTime.MinValue)
                .ToList();

            if (doneAssignments.Count > 0)
            {
                var lastDone = doneAssignments.OrderByDescending(a => a.ProcessedAt).First();
                userDisplayNames.TryGetValue(lastDone.ReceiverUserId, out finalSignerName);
                userOuNames.TryGetValue(lastDone.ReceiverUserId, out finalSignerDept);
                lastSignTime = lastDone.ProcessedAt;
            }

            var submitterId = instance?.CreatorId;
            string? submitterName = null;
            string? submitterDept = null;
            if (submitterId.HasValue)
            {
                userDisplayNames.TryGetValue(submitterId.Value, out submitterName);
                userOuNames.TryGetValue(submitterId.Value, out submitterDept);
            }

            string? workflowTemplateName = null;
            if (instance != null && workflowTemplateDict.TryGetValue(instance.WorkflowTemplateId, out var wtName))
            {
                workflowTemplateName = wtName;
            }

            masterDataDict.TryGetValue(doc.TypeId, out var typeName);
            latestTrinhByDoc.TryGetValue(doc.Id, out var signingContent);

            var (statusSummary, statusDetail) = MapSigningExportStatus(
                instance?.Status,
                finalSignerName,
                pendingSignerName,
                signedStepCount);

            DateTime? completedAt = null;
            if (instance != null
                && instance.Status == nameof(DocumentWorkflowInstanceStatus.COMPLETED))
            {
                completedAt = lastSignTime ?? (instance.FinishedAt > DateTime.MinValue ? instance.FinishedAt : null);
            }

            rows.Add(new Dictionary<string, object?>
            {
                [L["SigningExport.DocumentId"]] = doc.Id.ToString(),
                [L["SigningExport.StorageNumber"]] = doc.StorageNumber,
                [L["SigningExport.Title"]] = doc.Title,
                [L["SigningExport.WorkflowTemplate"]] = workflowTemplateName,
                [L["SigningExport.SigningContent"]] = signingContent,
                [L["SigningExport.WorkflowStartedAt"]] = FormatSigningExportDateTime(instance?.StartedAt),
                [L["SigningExport.WorkflowDeadline"]] = instance != null && instance.FinishedAt > DateTime.MinValue
                    ? FormatSigningExportDateTime(instance.FinishedAt)
                    : null,
                [L["SigningExport.CompletedAt"]] = FormatSigningExportDateTime(completedAt),
                [L["SigningExport.DocumentType"]] = typeName,
                [L["SigningExport.SubmitterName"]] = submitterName,
                [L["SigningExport.SubmitterDepartment"]] = submitterDept,
                [L["SigningExport.ReceiverUnit"]] = receiverUnit,
                [L["SigningExport.FinalSignerName"]] = finalSignerName,
                [L["SigningExport.FinalSignerDepartment"]] = finalSignerDept,
                [L["SigningExport.TotalSteps"]] = totalSteps > 0 ? totalSteps : null,
                [L["SigningExport.SignedStepCount"]] = signedStepCount,
                [L["SigningExport.LastSignTime"]] = FormatSigningExportDateTime(lastSignTime),
                [L["SigningExport.PendingSigner"]] = pendingSignerName,
                [L["SigningExport.StepChain"]] = stepChain,
                [L["SigningExport.StatusSummary"]] = statusSummary,
                [L["SigningExport.StatusDetail"]] = statusDetail
            });
        }

        return rows;
    }

    private static string? FormatSigningExportDateTime(DateTime? value)
    {
        if (!value.HasValue || value.Value <= DateTime.MinValue)
        {
            return null;
        }

        return value.Value.ToString("yyyy-MM-dd HH:mm:ss.fff");
    }

    private (string Summary, string Detail) MapSigningExportStatus(
        string? instanceStatus,
        string? finalSignerName,
        string? pendingSignerName,
        int signedStepCount)
    {
        if (string.IsNullOrEmpty(instanceStatus))
        {
            return ("Không xác định", "Không xác định");
        }

        switch (instanceStatus)
        {
            case nameof(DocumentWorkflowInstanceStatus.COMPLETED):
                var approvedDetail = string.IsNullOrWhiteSpace(finalSignerName)
                    ? "Đã duyệt"
                    : $"Đã duyệt - {finalSignerName}";
                return ("Đã duyệt", approvedDetail);

            case nameof(DocumentWorkflowInstanceStatus.RETURNED):
                var returnDetail = string.IsNullOrWhiteSpace(finalSignerName)
                    ? "Trả lại"
                    : $"Trả lại - {finalSignerName}";
                return ("Trả lại", returnDetail);

            case nameof(DocumentWorkflowInstanceStatus.REJECTED):
                var rejectDetail = string.IsNullOrWhiteSpace(finalSignerName)
                    ? "Từ chối"
                    : $"Từ chối - {finalSignerName}";
                return ("Từ chối", rejectDetail);

            case nameof(DocumentWorkflowInstanceStatus.DRAFT):
                return ("Lưu nháp", "Lưu nháp");

            case nameof(DocumentWorkflowInstanceStatus.CANCELLED):
                return ("Đã hủy", "Đã hủy");

            case nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS):
            case nameof(DocumentWorkflowInstanceStatus.OVERDUE):
                if (signedStepCount == 0)
                {
                    if (!string.IsNullOrWhiteSpace(pendingSignerName))
                    {
                        return ("Đang chờ ký", $"Chờ {pendingSignerName} ký");
                    }

                    return ("Đang chờ ký", "Chưa ai ký");
                }

                if (!string.IsNullOrWhiteSpace(pendingSignerName))
                {
                    return ("Đang chờ ký", $"Chờ {pendingSignerName} ký");
                }

                return ("Đang chờ ký", "Chưa ai ký");

            default:
                return ("Không xác định", "Không xác định");
        }
    }

    private async Task<Dictionary<Guid, string>> GetSigningExportUserDisplayNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var rows = await AsyncExecuter.ToListAsync(
            userQuery
                .Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Surname, u.Name, u.UserName, u.Email }));

        return rows.ToDictionary(
            u => u.Id,
            u =>
            {
                var full = $"{u.Surname} {u.Name}".Trim();
                return string.IsNullOrWhiteSpace(full) ? u.UserName ?? u.Email ?? u.Id.ToString() : full;
            });
    }

    private async Task<Dictionary<Guid, string>> GetSigningExportUserOrganizationUnitNamesAsync(IEnumerable<Guid> userIds)
    {
        var ids = userIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var organizationUnitRepository = LazyServiceProvider.LazyGetRequiredService<IRepository<OrganizationUnit, Guid>>();
        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var userOuRows = await AsyncExecuter.ToListAsync(
            userQuery
                .Where(u => ids.Contains(u.Id))
                .SelectMany(u => u.OrganizationUnits)
                .OrderBy(ou => ou.CreationTime)
                .Select(ou => new { ou.UserId, ou.OrganizationUnitId }));

        var primaryOuByUser = userOuRows
            .GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.First().OrganizationUnitId);

        var ouIds = primaryOuByUser.Values.Distinct().ToList();
        List<OrganizationUnit> organizationUnits;
        if (ouIds.Count > 0)
        {
            var ouQuery = await organizationUnitRepository.GetQueryableAsync();
            organizationUnits = await AsyncExecuter.ToListAsync(ouQuery.Where(x => ouIds.Contains(x.Id)));
        }
        else
        {
            organizationUnits = new List<OrganizationUnit>();
        }
        var ouNameById = organizationUnits.ToDictionary(x => x.Id, x => x.DisplayName);

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

    private static List<WorkflowStepTemplate> ResolveCommittedStepsOrderedForExport(
        DocumentWorkflowInstance instance,
        IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepTemplateDict,
        IReadOnlyDictionary<Guid, List<WorkflowStepTemplate>> legacyStepsByTemplateId)
    {
        var orderedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(instance.CommittedStepTemplateIdsJson);
        if (orderedIds is { Count: > 0 })
        {
            var ordered = new List<WorkflowStepTemplate>();
            foreach (var id in orderedIds)
            {
                if (stepTemplateDict.TryGetValue(id, out var step))
                {
                    ordered.Add(step);
                }
            }

            if (ordered.Count > 0)
            {
                return ordered;
            }
        }

        if (legacyStepsByTemplateId.TryGetValue(instance.WorkflowTemplateId, out var legacy))
        {
            return legacy;
        }

        return new List<WorkflowStepTemplate>();
    }

    /// <summary>
    /// Signing export is [AllowAnonymous] (browser navigation); user id comes from the one-time download token.
    /// </summary>
    private async Task<Guid> ValidateAndConsumeSigningExportTokenAsync(
        string inputToken,
        bool requestedExportAllUsers)
    {
        if (string.IsNullOrEmpty(inputToken))
        {
            throw new AbpAuthorizationException("Invalid download token.");
        }

        var cacheItem = await _downloadTokenCache.GetAsync(inputToken);
        if (cacheItem == null
            || cacheItem.Token != inputToken
            || !cacheItem.UserId.HasValue
            || cacheItem.ExportAllUsers != requestedExportAllUsers)
        {
            throw new AbpAuthorizationException("Invalid download token.");
        }

        await _downloadTokenCache.RemoveAsync(inputToken);
        return cacheItem.UserId.Value;
    }
}
