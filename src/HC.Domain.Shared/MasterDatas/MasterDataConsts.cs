namespace HC.MasterDatas;

public static class MasterDataConsts
{
    private const string DefaultSorting = "{0}SortOrder asc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "MasterData." : string.Empty);
    }

    public const int TypeMinLength = 1;
    public const int TypeMaxLength = 50;
    public const int CodeMinLength = 1;
    public const int CodeMaxLength = 50;
    public const int SortOrderMinLength = 0;
    public const int SortOrderMaxLength = 10000;
}