namespace HC.DocumentWorkflowInstances;

public class WorkflowSigningOptions
{
    public const string SectionName = "WorkflowSigning";

    /// <summary>
    /// Hours before FinishedAt when IN_PROGRESS workflows become eligible for extension.
    /// </summary>
    public int NearDeadlineHours { get; set; } = 24;
}
