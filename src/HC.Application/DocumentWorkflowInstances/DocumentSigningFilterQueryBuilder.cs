using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.DocumentAssignments;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.DocumentWorkflowInstances;

public class DocumentSigningFilterQueryBuilder : HCAppService, IDocumentSigningFilterQueryBuilder, ITransientDependency
{
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IWorkflowViewAccessService _workflowViewAccessService;
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;

    public DocumentSigningFilterQueryBuilder(
        IDocumentAssignmentRepository documentAssignmentRepository,
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IRepository<Document, Guid> documentRepository,
        IWorkflowViewAccessService workflowViewAccessService,
        IRepository<IdentityUser, Guid> identityUserRepository)
    {
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentRepository = documentRepository;
        _workflowViewAccessService = workflowViewAccessService;
        _identityUserRepository = identityUserRepository;
    }

    public async Task<SigningFilterState> BuildSigningFilterStateAsync(
        Guid currentUserId,
        string? filterText,
        DocumentSigningFilterMode filterMode,
        DocumentSigningDateFilterField dateFilterField,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? focusDocumentId,
        Guid? submitterUserId = null,
        Guid? submitterOrganizationUnitId = null)
    {
        var stopwatch = Stopwatch.StartNew();

        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var instanceQueryable = await _documentWorkflowInstanceRepository.GetQueryableAsync();
        var documentQueryable = await _documentRepository.GetQueryableAsync();

        var workflowDocumentQuery = documentQueryable.Where(d => d.SourceType == DocumentSourceType.Workflow);

        var receivedViaAssignmentDocIds = await AsyncExecuter.ToListAsync(
            assignmentQueryable
                .Where(a => a.ReceiverUserId == currentUserId && a.WorkflowStepTemplateId != null)
                .Join(workflowDocumentQuery, a => a.DocumentId, d => d.Id, (a, d) => d.Id)
                .Distinct());

        var receivedViaViewAccessDocIds = await _workflowViewAccessService.GetViewEligibleDocumentIdsAsync(currentUserId);
        var receivedDocIdSet = receivedViaAssignmentDocIds
            .Concat(receivedViaViewAccessDocIds)
            .ToHashSet();

        var receivedDocIdQuery = receivedDocIdSet.Count == 0
            ? documentQueryable.Where(d => false).Select(d => d.Id)
            : documentQueryable.Where(d => receivedDocIdSet.Contains(d.Id)).Select(d => d.Id);

        var initiatedDocIdQuery = instanceQueryable
            .Where(i => i.CreatorId == currentUserId)
            .Join(workflowDocumentQuery, i => i.DocumentId, d => d.Id, (i, d) => d.Id)
            .Distinct();

        var sentToOthersDocIdQuery = assignmentQueryable
            .Where(a => a.ReceiverUserId != currentUserId && a.WorkflowStepTemplateId != null)
            .Join(workflowDocumentQuery, a => a.DocumentId, d => d.Id, (a, d) => d.Id)
            .Distinct();

        var sentByMeCandidateQuery = initiatedDocIdQuery.Intersect(sentToOthersDocIdQuery);
        var sentByMeCandidateList = await AsyncExecuter.ToListAsync(sentByMeCandidateQuery);
        var excludeSentByMeBecauseCreatorSigned =
            await GetSentByMeExcludeBecauseCreatorSignedAsync(currentUserId, sentByMeCandidateList);
        var sentByMeAllowedSet = sentByMeCandidateList
            .Where(id => !excludeSentByMeBecauseCreatorSigned.Contains(id))
            .ToHashSet();

        var allDocIdQuery = receivedDocIdQuery.Union(sentByMeCandidateQuery);
        var baseDocQuery = documentQueryable.Where(d => allDocIdQuery.Contains(d.Id));
        baseDocQuery = ApplySigningDateFilter(baseDocQuery, instanceQueryable, dateFilterField, fromDate, toDate);

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            var trimmed = filterText.Trim();
            baseDocQuery = baseDocQuery.Where(d =>
                (d.Title != null && d.Title.Contains(trimmed)) ||
                (d.No != null && d.No.Contains(trimmed)) ||
                (d.StorageNumber != null && d.StorageNumber.Contains(trimmed)));
        }

        if (submitterUserId.HasValue && submitterUserId.Value != Guid.Empty)
        {
            var submitterDocIdsQuery = instanceQueryable
                .Where(i => i.CreatorId == submitterUserId.Value)
                .Select(i => i.DocumentId)
                .Distinct();
            baseDocQuery = baseDocQuery.Where(d => submitterDocIdsQuery.Contains(d.Id));
        }

        if (submitterOrganizationUnitId.HasValue && submitterOrganizationUnitId.Value != Guid.Empty)
        {
            // Materialize identity users first — Identity and HC use separate DbContext instances.
            var userQueryable = await _identityUserRepository.GetQueryableAsync();
            var creatorIdsInOu = await AsyncExecuter.ToListAsync(
                userQueryable
                    .Where(u => u.OrganizationUnits.Any(ou => ou.OrganizationUnitId == submitterOrganizationUnitId.Value))
                    .Select(u => u.Id));

            if (creatorIdsInOu.Count == 0)
            {
                baseDocQuery = baseDocQuery.Where(d => false);
            }
            else
            {
                var submitterOuDocIds = await AsyncExecuter.ToListAsync(
                    instanceQueryable
                        .Where(i => i.CreatorId.HasValue && creatorIdsInOu.Contains(i.CreatorId.Value))
                        .Select(i => i.DocumentId)
                        .Distinct());
                baseDocQuery = submitterOuDocIds.Count == 0
                    ? baseDocQuery.Where(d => false)
                    : baseDocQuery.Where(d => submitterOuDocIds.Contains(d.Id));
            }
        }

        var filteredDocIds = baseDocQuery.Select(d => d.Id);
        var sentToMeCount = await AsyncExecuter.CountAsync(
            filteredDocIds.Where(id => receivedDocIdSet.Contains(id)));
        var sentByMeCount = sentByMeAllowedSet.Count == 0
            ? 0
            : await AsyncExecuter.CountAsync(
                filteredDocIds.Where(id => sentByMeAllowedSet.Contains(id)));
        const int followingCount = 0;
        var allCount = await AsyncExecuter.CountAsync(filteredDocIds);

        IQueryable<Document> modeFilteredQuery;
        switch (filterMode)
        {
            case DocumentSigningFilterMode.SentToMe:
                modeFilteredQuery = receivedDocIdSet.Count == 0
                    ? baseDocQuery.Where(d => false)
                    : baseDocQuery.Where(d => receivedDocIdSet.Contains(d.Id));
                break;
            case DocumentSigningFilterMode.SentByMe:
                modeFilteredQuery = sentByMeAllowedSet.Count == 0
                    ? baseDocQuery.Where(d => false)
                    : baseDocQuery.Where(d => sentByMeAllowedSet.Contains(d.Id));
                break;
            case DocumentSigningFilterMode.Following:
                modeFilteredQuery = baseDocQuery.Where(d => false);
                break;
            default:
                modeFilteredQuery = baseDocQuery;
                break;
        }

        if (focusDocumentId.HasValue)
        {
            modeFilteredQuery = modeFilteredQuery.Where(d => d.Id == focusDocumentId.Value);
        }

        var myPendingAssignmentDocIdsQuery = assignmentQueryable
            .Where(a =>
                a.ReceiverUserId == currentUserId
                && a.WorkflowStepTemplateId != null
                && a.Status == nameof(DocumentAssignmentStatus.PENDING)
                && a.IsCurrent)
            .Select(a => a.DocumentId)
            .Distinct();

        stopwatch.Stop();
        if (stopwatch.ElapsedMilliseconds >= 500)
        {
            Logger.LogInformation(
                "BuildSigningFilterStateAsync completed in {ElapsedMs}ms for user {UserId}, mode={FilterMode}, allCount={AllCount}",
                stopwatch.ElapsedMilliseconds,
                currentUserId,
                filterMode,
                allCount);
        }

        return new SigningFilterState
        {
            ModeFilteredQuery = modeFilteredQuery,
            InstanceQueryable = instanceQueryable,
            MyPendingAssignmentDocIdsQuery = myPendingAssignmentDocIdsQuery,
            AllCount = allCount,
            SentToMeCount = sentToMeCount,
            SentByMeCount = sentByMeCount,
            FollowingCount = followingCount
        };
    }

    private async Task<HashSet<Guid>> GetSentByMeExcludeBecauseCreatorSignedAsync(
        Guid currentUserId,
        List<Guid> candidateDocumentIds)
    {
        var exclude = new HashSet<Guid>();
        if (candidateDocumentIds.Count == 0)
        {
            return exclude;
        }

        var instanceQueryable = await _documentWorkflowInstanceRepository.GetQueryableAsync();
        var instances = await AsyncExecuter.ToListAsync(
            instanceQueryable.Where(i =>
                candidateDocumentIds.Contains(i.DocumentId) && i.CreatorId == currentUserId));

        var latestStartedByDoc = instances
            .GroupBy(i => i.DocumentId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(x => x.StartedAt).ThenByDescending(x => x.Id).First().StartedAt);

        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var signedAssignments = await AsyncExecuter.ToListAsync(
            assignmentQueryable.Where(a =>
                candidateDocumentIds.Contains(a.DocumentId)
                && a.ReceiverUserId == currentUserId
                && a.WorkflowStepTemplateId != null
                && a.Status == nameof(DocumentAssignmentStatus.DONE)));

        foreach (var assignment in signedAssignments)
        {
            if (latestStartedByDoc.TryGetValue(assignment.DocumentId, out var startedAt)
                && assignment.CreationTime >= startedAt)
            {
                exclude.Add(assignment.DocumentId);
            }
        }

        return exclude;
    }

    private static IQueryable<Document> ApplySigningDateFilter(
        IQueryable<Document> baseDocQuery,
        IQueryable<DocumentWorkflowInstance> instanceQueryable,
        DocumentSigningDateFilterField dateFilterField,
        DateTime? fromDate,
        DateTime? toDate)
    {
        if (!fromDate.HasValue && !toDate.HasValue)
        {
            return baseDocQuery;
        }

        if (dateFilterField == DocumentSigningDateFilterField.IncomingDate)
        {
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                baseDocQuery = baseDocQuery.Where(d => d.IncommingDate >= from);
            }

            if (toDate.HasValue)
            {
                var toEnd = toDate.Value.Date.AddDays(1).AddSeconds(-1);
                baseDocQuery = baseDocQuery.Where(d => d.IncommingDate <= toEnd);
            }

            return baseDocQuery;
        }

        var latestStartedAtByDoc = instanceQueryable
            .GroupBy(i => i.DocumentId)
            .Select(g => new { DocumentId = g.Key, StartedAt = g.Max(x => x.StartedAt) });

        var latestInstancesQuery =
            from i in instanceQueryable
            join l in latestStartedAtByDoc on new { i.DocumentId, i.StartedAt } equals new { l.DocumentId, l.StartedAt }
            select i;

        if (fromDate.HasValue)
        {
            var from = fromDate.Value.Date;
            latestInstancesQuery = latestInstancesQuery.Where(i => i.StartedAt >= from);
        }

        if (toDate.HasValue)
        {
            var toEnd = toDate.Value.Date.AddDays(1).AddSeconds(-1);
            latestInstancesQuery = latestInstancesQuery.Where(i =>
                i.FinishedAt > DateTime.MinValue && i.FinishedAt <= toEnd);
        }

        var matchingDocIds = latestInstancesQuery.Select(i => i.DocumentId).Distinct();
        return baseDocQuery.Where(d => matchingDocIds.Contains(d.Id));
    }
}
