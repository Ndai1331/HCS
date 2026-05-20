using HC.WorkflowStepTemplates;
using Volo.Abp.Identity;
using System;
using System.Collections.Generic;
using HC.WorkflowStepAssignments;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentWithNavigationPropertiesBase
{
    public WorkflowStepAssignment WorkflowStepAssignment { get; set; } = null!;
    public WorkflowStepTemplate? Step { get; set; }

    public IdentityUser? DefaultUser { get; set; }

    public IdentityRole? Role { get; set; }
}