using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentCreateDtoBase
{
    public bool IsPrimary { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public Guid? StepId { get; set; }

    public Guid? DefaultUserId { get; set; }

    public string AssigneeType { get; set; } = WorkflowStepAssigneeTypeNames.SpecificUser;

    public Guid? RoleId { get; set; }
}