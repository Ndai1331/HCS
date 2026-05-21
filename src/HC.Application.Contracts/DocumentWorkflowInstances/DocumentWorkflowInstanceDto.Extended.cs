using System;

namespace HC.DocumentWorkflowInstances;

public class DocumentWorkflowInstanceDto : DocumentWorkflowInstanceDtoBase
{
    public DateTime? OverdueAt { get; set; }

    public int ExtensionCount { get; set; }

    public int TotalExtensionBusinessDays { get; set; }
}