namespace HC.Documents;

/// <summary>
/// Enum for Document status codes matching MasterData records
/// (MasterData.Type = "TRANG_THAI_VB").
/// Use GetCode() extension to get the MasterData Code string.
/// </summary>
public enum DocumentStatusCode
{
    /// <summary>Đã gửi</summary>
    DA_GUI,

    /// <summary>Đang xử lý</summary>
    DANG_XU_LY,

    /// <summary>Hoàn thành</summary>
    HT,

    /// <summary>Đã hủy</summary>
    DA_HUY
}

/// <summary>
/// Extension methods for DocumentStatusCode enum
/// </summary>
public static class DocumentStatusCodeExtensions
{
    /// <summary>
    /// Get the MasterData Code string for a DocumentStatusCode.
    /// </summary>
    public static string GetCode(this DocumentStatusCode statusCode)
    {
        return statusCode switch
        {
            DocumentStatusCode.DA_GUI => "DA_GUI",
            DocumentStatusCode.DANG_XU_LY => "DANG_XU_LY",
            DocumentStatusCode.HT => "HT",
            DocumentStatusCode.DA_HUY => "DA_HUY",
            _ => throw new System.ArgumentOutOfRangeException(nameof(statusCode), statusCode, "Unknown DocumentStatusCode")
        };
    }
}
