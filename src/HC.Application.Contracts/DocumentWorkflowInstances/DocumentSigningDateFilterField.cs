namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Which date range applies when exporting or filtering the signing list.
/// </summary>
public enum DocumentSigningDateFilterField
{
    /// <summary>Filter by Document.IncommingDate (default, matches grid list).</summary>
    IncomingDate = 0,

    /// <summary>Filter by latest workflow instance StartedAt / FinishedAt (deadline window).</summary>
    WorkflowDeadline = 1
}
