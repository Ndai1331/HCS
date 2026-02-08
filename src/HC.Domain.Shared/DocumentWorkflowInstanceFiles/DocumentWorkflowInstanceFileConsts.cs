namespace HC.DocumentWorkflowInstanceFiles;

public static class DocumentWorkflowInstanceFileConsts
{
    private const string DefaultSorting = "{0}Id asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DocumentWorkflowInstanceFile." : string.Empty);
    }
}