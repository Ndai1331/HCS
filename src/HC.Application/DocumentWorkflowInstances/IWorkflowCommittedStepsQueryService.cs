using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowCommittedStepsQueryService
{
    Task<List<WorkflowStepTemplate>> LoadCommittedWorkflowStepsOrderedAsync(DocumentWorkflowInstance instance);
}
