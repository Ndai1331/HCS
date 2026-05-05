namespace HC.Reports;

public static class ReportConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Report." : string.Empty);
    }

    public const int NameMaxLength = 255;
    public const int UrlMaxLength = 1000;
    public const int ImageMaxLength = 255;
}