using HC.WorkflowStepTemplates;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.WorkflowStepAssignments;

public abstract class WorkflowStepAssignmentWithNavigationPropertiesDtoBase
{
    public WorkflowStepAssignmentDto WorkflowStepAssignment { get; set; } = null!;
    public WorkflowStepTemplateDto? Step { get; set; }

    public IdentityUserDto? DefaultUser { get; set; }

    public IdentityRoleDto? Role { get; set; }
}