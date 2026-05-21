using System;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstances;

public class WorkflowExtensionSummaryDto
{
    public int ExtensionCount { get; set; }

    public int TotalExtensionBusinessDays { get; set; }

    public List<WorkflowExtensionHistoryItemDto> History { get; set; } = new();
}

public class WorkflowExtensionHistoryItemDto
{
    public Guid Id { get; set; }

    public DateTime CreationTime { get; set; }

    public Guid ExtendedByUserId { get; set; }

    public string? ExtendedByUserName { get; set; }

    public int ExtensionBusinessDays { get; set; }

    public DateTime PreviousFinishedAt { get; set; }

    public DateTime NewFinishedAt { get; set; }

    public string Reason { get; set; } = null!;
}
