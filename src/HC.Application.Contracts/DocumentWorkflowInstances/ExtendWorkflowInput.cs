using System;
using System.ComponentModel.DataAnnotations;

namespace HC.DocumentWorkflowInstances;

public class ExtendWorkflowInput
{
    public Guid WorkflowInstanceId { get; set; }

    [Range(1, 365)]
    public int ExtensionBusinessDays { get; set; }

    [Required]
    public string Reason { get; set; } = null!;
}
