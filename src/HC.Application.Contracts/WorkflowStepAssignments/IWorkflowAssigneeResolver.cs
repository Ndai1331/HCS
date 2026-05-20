using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.WorkflowStepAssignments;

public interface IWorkflowAssigneeResolver
{
    Task<Guid?> GetSubmitterPrimaryOrganizationUnitIdAsync(Guid submitterUserId);

    Task<List<WorkflowStepUserDto>> ResolveCandidatesByRoleAsync(Guid roleId, Guid submitterUserId, bool isPrimary = false);
}
