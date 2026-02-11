using System;
using System.Collections.Generic;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// DTO containing workflow step details with assigned users
/// </summary>
public class WorkflowStepDetailDto
{
    public Guid StepId { get; set; }
    public int Order { get; set; }
    public string Name { get; set; } = null!;
    public string Type { get; set; } = null!;
    public int? SLADays { get; set; }
    public bool AllowReturn { get; set; }
    public List<WorkflowStepUserDto> AssignedUsers { get; set; } = new();
}

/// <summary>
/// DTO for a user assigned to a workflow step
/// </summary>
public class WorkflowStepUserDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; } = null!;
    public string? FullName { get; set; }
    public bool IsPrimary { get; set; }
}

/// <summary>
/// DTO containing full workflow info for the submit modal
/// </summary>
public class WorkflowSubmitInfoDto
{
    public Guid WorkflowId { get; set; }
    public string WorkflowName { get; set; } = null!;
    public Guid WorkflowTemplateId { get; set; }
    public string WorkflowTemplateName { get; set; } = null!;
    public string? PdfTemplatePath { get; set; }
    public bool HasTemplateFile { get; set; }
    /// <summary>
    /// Signing mode: SEQUENTIAL (default) or PARALLEL.
    /// SEQUENTIAL: step-by-step signing, each step completes before the next begins.
    /// PARALLEL: all steps are created at once, all users can sign simultaneously.
    /// </summary>
    public string? SignMode { get; set; }
    public List<WorkflowStepDetailDto> Steps { get; set; } = new();
}

/// <summary>
/// DTO for the active workflow instance of a document (for UI display)
/// </summary>
public class DocumentWorkflowStatusDto
{
    public Guid DocumentWorkflowInstanceId { get; set; }
    public Guid DocumentId { get; set; }
    public string Status { get; set; } = null!;
    public Guid CurrentStepId { get; set; }
    public string CurrentStepName { get; set; } = null!;
    public int CurrentStepOrder { get; set; }
    public int TotalSteps { get; set; }
    public DateTime StartedAt { get; set; }
    public string WorkflowName { get; set; } = null!;

    /// <summary>
    /// The current user's assignment for this workflow instance (if any)
    /// </summary>
    public DocumentAssignmentInfoDto? MyAssignment { get; set; }
}

/// <summary>
/// Simplified DTO for displaying assignment info
/// </summary>
public class DocumentAssignmentInfoDto
{
    public Guid AssignmentId { get; set; }
    public string Status { get; set; } = null!;
    public string ActionType { get; set; } = null!;
    public int StepOrder { get; set; }
    public string StepName { get; set; } = null!;
    public bool IsCurrent { get; set; }
    public bool CanAct { get; set; }
}
