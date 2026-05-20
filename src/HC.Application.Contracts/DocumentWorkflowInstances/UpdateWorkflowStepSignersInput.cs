using System;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstances;

public class UpdateWorkflowStepSignersInput
{
    public Guid WorkflowInstanceId { get; set; }

    public List<WorkflowStepSignerSelectionDto> StepSignerSelections { get; set; } = new();
}
