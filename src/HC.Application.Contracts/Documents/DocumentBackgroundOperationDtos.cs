using System;

namespace HC.Documents;

/// <summary>
/// Returned when a long-running document operation is queued (HTTP 202).
/// </summary>
public class QueueDocumentBackgroundOperationResultDto
{
    public Guid OperationId { get; set; }
}

public class DocumentBackgroundOperationStatusDto
{
    public Guid OperationId { get; set; }
    public string OperationType { get; set; } = null!;
    public string Status { get; set; } = null!;
    public int Progress { get; set; }
    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
    public Guid? DocumentId { get; set; }
}
