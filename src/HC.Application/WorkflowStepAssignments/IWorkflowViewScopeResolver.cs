using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.WorkflowStepAssignments;

public interface IWorkflowViewScopeResolver
{
    Task<HashSet<Guid>> ResolveViewerUserIdsAsync(
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        WorkflowViewScopeData? instanceScope,
        Guid? submitterUserId);
}
