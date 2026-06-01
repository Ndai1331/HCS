using System;
using System.Linq;
using HC.Documents;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Composable LINQ state for document signing list filters (tab counts + mode-scoped document query).
/// </summary>
public sealed class SigningFilterState
{
    public required IQueryable<Document> ModeFilteredQuery { get; init; }

    public required IQueryable<DocumentWorkflowInstance> InstanceQueryable { get; init; }

    public required IQueryable<Guid> MyPendingAssignmentDocIdsQuery { get; init; }

    public int AllCount { get; init; }

    public int SentToMeCount { get; init; }

    public int SentByMeCount { get; init; }

    public int FollowingCount { get; init; }
}
