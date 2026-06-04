using System;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstances;

public class WorkflowStepViewScopeSelectionDto
{
    public Guid StepId { get; set; }

    public List<Guid> OrganizationUnitIds { get; set; } = new();

    public List<Guid> UserIds { get; set; } = new();
}
