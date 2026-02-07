namespace HC.DocumentWorkflowInstanceLogss;

public static class DocumentWorkflowInstanceLogsConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "DocumentWorkflowInstanceLogs." : string.Empty);
    }

    public const int ActionMaxLength = 30;
    public const int ActorRoleMaxLength = 30;
    public const int FromStatusMaxLength = 30;
    public const int ToStatusMaxLength = 30;
}