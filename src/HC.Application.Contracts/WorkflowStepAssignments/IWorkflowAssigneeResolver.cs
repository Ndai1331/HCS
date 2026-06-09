using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.WorkflowStepAssignments;

public interface IWorkflowAssigneeResolver
{
    Task<Guid?> GetSubmitterPrimaryOrganizationUnitIdAsync(Guid submitterUserId);

    Task<IReadOnlyList<Guid>> GetOrganizationUnitScopeIdsForUserAsync(Guid userId);

    /// <summary>
    /// Builds the full organization-unit scope for a user: for every OU the user belongs to,
    /// includes that OU, all of its ancestors (parent chain) and all of its descendants (children).
    /// </summary>
    Task<IReadOnlyList<Guid>> GetOrganizationUnitScopeWithDescendantsForUserAsync(Guid userId);

    Task<List<WorkflowStepUserDto>> ResolveCandidatesByRoleAsync(Guid roleId, Guid submitterUserId, bool isPrimary = false);

    Task<List<WorkflowStepUserDto>> ResolveCandidatesByRoleInOrganizationUnitsAsync(
        Guid roleId,
        IReadOnlyList<Guid> organizationUnitIds,
        bool isPrimary = false);
}
