namespace HC.DocumentWorkflowInstances;

public static class DocumentWorkflowInstanceConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DocumentWorkflowInstance." : string.Empty);
    }

    public const int StatusMaxLength = 20;

    /// <summary>JSON array of step template Ids (submission order) — frozen at submit/resubmit so template edits do not affect in-flight instances.</summary>
    public const int CommittedStepTemplateIdsJsonMaxLength = 8000;
}