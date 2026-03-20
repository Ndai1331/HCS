namespace HC.Documents;

public class DocumentWithNavigationPropertiesDto : DocumentWithNavigationPropertiesDtoBase
{
    /// <summary>
    /// Resolved display for <see cref="DocumentDto.FromUserId"/> (SentToMe list).
    /// </summary>
    public string? FromUserDisplayName { get; set; }

    /// <summary>
    /// Resolved display for <see cref="DocumentDto.ReceiverUserId"/>.
    /// </summary>
    public string? ReceiverUserDisplayName { get; set; }

    /// <summary>
    /// Resolved display for <see cref="DocumentDto.DepartmentId"/>.
    /// </summary>
    public string? DepartmentDisplayName { get; set; }

    /// <summary>
    /// When true, hide &quot;Submit for signing&quot; on manage-documents (parent has workflow copy IN_PROGRESS or COMPLETED).
    /// </summary>
    public bool HideSubmitForSigningButton { get; set; }
}