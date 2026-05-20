using System;

namespace HC.DocumentWorkflowInstances;

public class WorkflowStepSignerSelectionDto
{
    public Guid StepId { get; set; }

    public Guid SelectedUserId { get; set; }
}
