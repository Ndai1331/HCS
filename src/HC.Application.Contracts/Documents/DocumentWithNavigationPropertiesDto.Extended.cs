namespace HC.Documents;

public class DocumentWithNavigationPropertiesDto : DocumentWithNavigationPropertiesDtoBase
{
    /// <summary>
    /// True when document was sent to current user (has DocumentAssignment as receiver).
    /// Used in Personal Documents (sourceType=1) to show forward arrow icon.
    /// </summary>
    public bool IsSentToMe { get; set; }
}