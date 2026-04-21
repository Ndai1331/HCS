using System;

namespace HC.Documents;

/// <summary>
/// Published over the distributed bus so Blazor can push SignalR progress to the initiating user.
/// </summary>
[Serializable]
public class DocumentBackgroundOperationProgressEto
{
    public Guid OperationId { get; set; }
    public Guid UserId { get; set; }
    public Guid? TenantId { get; set; }
    public string OperationType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? DocumentId { get; set; }
    public string? DocumentNo { get; set; }
    public string? DocumentTitle { get; set; }
    public string? OperationTypeDisplay { get; set; }
}
