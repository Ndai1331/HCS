using HC.WorkflowDefinitions;
using System;
using System.Collections.Generic;
using HC.Workflows;

namespace HC.Workflows;

public abstract class WorkflowWithNavigationPropertiesBase
{
    public Workflow Workflow { get; set; } = null!;
    // Left join in repository: missing definition row yields null (e.g. orphaned WorkflowDefinitionId).
    public WorkflowDefinition? WorkflowDefinition { get; set; }
}