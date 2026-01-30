namespace HC.DocumentAssignments;

public static class DocumentAssignmentConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DocumentAssignment." : string.Empty);
    }

    public const int StepOrderMinLength = 0;
    public const int StepOrderMaxLength = 20;
    public const int ActionTypeMaxLength = 20;
    public const int StatusMaxLength = 20;
}