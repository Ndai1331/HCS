namespace HC.DocumentWorkflowInstanceLogss;

public class WorkflowChartStatisticsDto
{
    /// <summary>Workflow signing documents where the current user has completed at least one step (DONE assignment).</summary>
    public int SignedCount { get; set; }

    /// <summary>Workflow signing documents where the current user has a pending current step assignment.</summary>
    public int UnsignedCount { get; set; }

    /// <summary>Workflow signing documents where the user rejected (instance REJECTED) or assignments were revoked after overdue cancel (instance CANCELLED).</summary>
    public int ReturnedOrRejectedCount { get; set; }

    public int TotalCount { get; set; }
}
