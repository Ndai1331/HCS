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

    /// <summary>
    /// Signing method ID (required when action is APPROVE).
    /// References MasterData with Type = "LOAI_KY".
    /// </summary>
    public Guid? SigningMethodId { get; set; }

    /// <summary>
    /// Selected user signature ID (optional).
    /// Used when user has multiple active/valid signatures for the same sign type.
    /// </summary>
    public Guid? UserSignatureId { get; set; }

    /// <summary>
    /// When advancing to the next SEQUENTIAL step with multiple role-based candidates, the selected receiver user id.
    /// </summary>
    public Guid? NextStepSignerUserId { get; set; }
}
