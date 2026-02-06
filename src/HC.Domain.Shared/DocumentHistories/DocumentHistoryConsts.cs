namespace HC.DocumentHistories;

public static class DocumentHistoryConsts
{
    // Default sorting for repository with navigation properties - no prefix needed
    private const string DefaultSorting = "DocumentHistory.CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        // For DocumentHistoryWithNavigationProperties, we don't use prefix
        return DefaultSorting;
    }

    public const int ActionMaxLength = 30;
}