using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.DocumentAssignments;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public class DocumentSigningFilterQueryBuilder : HCAppService, IDocumentSigningFilterQueryBuilder, ITransientDependency
{
    private readonly IDocumentAssignmentRepository _documentAssignmentRepository;
    private readonly IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    private readonly IRepository<Document, Guid> _documentRepository;

    public DocumentSigningFilterQueryBuilder(
        IDocumentAssignmentRepository documentAssignmentRepository,
        IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository,
        IRepository<Document, Guid> documentRepository)
    {
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentRepository = documentRepository;
    }

    public async Task<SigningFilterState> BuildSigningFilterStateAsync(
        Guid currentUserId,
        string? filterText,
        DocumentSigningFilterMode filterMode,
        DocumentSigningDateFilterField dateFilterField,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? focusDocumentId)
    {
        var assignmentQueryable = await _documentAssignmentRepository.GetQueryableAsync();
        var instanceQueryable = await _documentWorkflowInstanceRepository.GetQueryableAsync();
        var documentQueryable = await _documentRepository.GetQueryableAsync();

        var workflowDocumentQuery = documentQueryable.Where(d => d.SourceType == DocumentSourceType.Workflow);

        var receivedDocIdQuery = assignmentQueryable
            .Where(a => a.ReceiverUserId == currentUserId && a.WorkflowStepTemplateId != null)
            .Join(workflowDocumentQuery, a => a.DocumentId, d => d.Id, (a, d) => d.Id)
            .Distinct();

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

        var filteredDocIds = baseDocQuery.Select(d => d.Id);
        var sentToMeCount = await AsyncExecuter.CountAsync(
            filteredDocIds.Where(id => receivedDocIdQuery.Contains(id)));
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
                modeFilteredQuery = baseDocQuery.Where(d => receivedDocIdQuery.Contains(d.Id));
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
