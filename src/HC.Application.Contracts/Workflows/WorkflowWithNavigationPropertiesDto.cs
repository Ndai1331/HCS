using HC.WorkflowDefinitions;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.Workflows;

public abstract class WorkflowWithNavigationPropertiesDtoBase
{
    public WorkflowDto Workflow { get; set; } = null!;
    public WorkflowDefinitionDto? WorkflowDefinition { get; set; }
}