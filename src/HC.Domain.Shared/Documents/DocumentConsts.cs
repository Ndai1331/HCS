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
    /// Văn bản của tôi - Personal documents
    /// </summary>
    [Description("Personal")]
    Personal = 1,

    /// <summary>
    /// Văn bản tạo từ quy trình trình ký - Workflow generated documents
    /// </summary>
    [Description("Workflow")]
    Workflow = 3
}
