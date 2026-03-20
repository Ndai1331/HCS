using System.ComponentModel;

namespace HC.Documents;

public static class DocumentConsts
{
    private const string DefaultSorting = "{0}CreationTime desc";

    public static string GetDefaultSorting(bool withEntityName)
    {
        return string.Format(DefaultSorting, withEntityName ? "Document." : string.Empty);
    }

    public const int NoMaxLength = 50;
    public const int CurrentStatusMaxLength = 30;
    public const int StorageNumberMaxLength = 50;
}



public enum DocumentSourceType
{
    /// <summary>
    /// Văn thư lưu trữ - Archival documents
    /// </summary>
    [Description("Archive")]
    Archive = 0,

    /// <summary>
    /// Văn bản của tôi - Personal documents (created by current user)
    /// </summary>
    [Description("Personal")]
    Personal = 1,

    /// <summary>
    /// Văn bản gửi tới tôi - Inbox (individual or department routing; optional denormalized fields on Document)
    /// </summary>
    [Description("SentToMe")]
    SentToMe = 2,

    /// <summary>
    /// Văn bản trình ký (workflow) - Document signing / workflow pipeline
    /// </summary>
    [Description("Workflow")]
    Workflow = 3
}
