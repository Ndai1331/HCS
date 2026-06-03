using System;
using System.ComponentModel.DataAnnotations;

namespace HC.DocumentWorkflowInstances;

public class CancelWorkflowByInitiatorInput
{
    [Required]
    public Guid WorkflowInstanceId { get; set; }

    public string? Reason { get; set; }
}
