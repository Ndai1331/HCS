using System;
using System.ComponentModel.DataAnnotations;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Input DTO for processing a workflow action (Approve/Return/Reject)
/// </summary>
public class WorkflowActionInput
{
    [Required]
    public Guid DocumentWorkflowInstanceId { get; set; }

    [Required]
    public Guid DocumentAssignmentId { get; set; }

    /// <summary>
    /// Action: APPROVED, RETURNED, REJECTED
    /// </summary>
    [Required]
    public string Action { get; set; } = null!;

    /// <summary>
    /// Optional note/comment for the action
    /// </summary>
    public string? Note { get; set; }
}
