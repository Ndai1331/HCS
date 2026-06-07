using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;

namespace HC.WorkflowStepAssignments;

public class WorkflowViewScopeResolver : IWorkflowViewScopeResolver, ITransientDependency
{
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IWorkflowAssigneeResolver _workflowAssigneeResolver;
    private readonly IAsyncQueryableExecuter _asyncExecuter;

    public WorkflowViewScopeResolver(
        IRepository<IdentityUser, Guid> identityUserRepository,
        IWorkflowAssigneeResolver workflowAssigneeResolver,
        IAsyncQueryableExecuter asyncExecuter)
    {
        _identityUserRepository = identityUserRepository;
        _workflowAssigneeResolver = workflowAssigneeResolver;
        _asyncExecuter = asyncExecuter;
    }

    public async Task<HashSet<Guid>> ResolveViewerUserIdsAsync(
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        WorkflowViewScopeData? instanceScope,
        Guid? submitterUserId)
    {
        var result = new HashSet<Guid>();
        if (stepAssignments == null || stepAssignments.Count == 0)
        {
            return result;
        }

        var ouIds = new HashSet<Guid>();
        var directUserIds = new HashSet<Guid>();
        var roleRequests = new List<(Guid RoleId, bool IsPrimary)>();

        foreach (var assignment in stepAssignments.Where(a => a.IsActive))
        {
            MergeTemplateScope(assignment, ouIds, directUserIds, roleRequests);
        }

        if (instanceScope != null)
        {
            foreach (var ouId in instanceScope.OrganizationUnitIds.Where(x => x != Guid.Empty))
            {
                ouIds.Add(ouId);
            }

            foreach (var userId in instanceScope.UserIds.Where(x => x != Guid.Empty))
            {
                directUserIds.Add(userId);
            }
        }

        foreach (var userId in directUserIds)
        {
            result.Add(userId);
        }

        if (ouIds.Count > 0)
        {
            var members = await ResolveActiveUserIdsInOrganizationUnitsAsync(ouIds.ToList());
            foreach (var userId in members)
            {
                result.Add(userId);
            }
        }

        foreach (var roleRequest in roleRequests.Distinct())
        {
            List<WorkflowStepUserDto> candidates;
            if (ouIds.Count > 0)
            {
                candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleInOrganizationUnitsAsync(
                    roleRequest.RoleId,
                    ouIds.ToList(),
                    roleRequest.IsPrimary);
            }
            else if (submitterUserId.HasValue)
            {
                candidates = await _workflowAssigneeResolver.ResolveCandidatesByRoleAsync(
                    roleRequest.RoleId,
                    submitterUserId.Value,
                    roleRequest.IsPrimary);
            }
            else
            {
                continue;
            }

            foreach (var candidate in candidates)
            {
                result.Add(candidate.UserId);
            }
        }

        return result;
    }

    private static void MergeTemplateScope(
        WorkflowStepAssignment assignment,
        HashSet<Guid> ouIds,
        HashSet<Guid> directUserIds,
        List<(Guid RoleId, bool IsPrimary)> roleRequests)
    {
        foreach (var ouId in WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(assignment.OrganizationUnitIdsJson))
        {
            ouIds.Add(ouId);
        }

        foreach (var userId in WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(
                     assignment.DefaultUserIdsJson,
                     assignment.DefaultUserId))
        {
            directUserIds.Add(userId);
        }

        if (!assignment.RoleId.HasValue || assignment.RoleId == Guid.Empty)
        {
            return;
        }

        var isLegacySubmitterRole = string.Equals(
            assignment.AssigneeType,
            WorkflowStepAssigneeTypeNames.RoleInSubmitterOrganizationUnit,
            StringComparison.OrdinalIgnoreCase);

        var isScoped = string.Equals(
            assignment.AssigneeType,
            WorkflowStepAssigneeTypeNames.ScopedAssignee,
            StringComparison.OrdinalIgnoreCase);

        if (isLegacySubmitterRole || isScoped)
        {
            roleRequests.Add((assignment.RoleId.Value, assignment.IsPrimary));
        }
    }

    private async Task<List<Guid>> ResolveActiveUserIdsInOrganizationUnitsAsync(IReadOnlyList<Guid> organizationUnitIds)
    {
        if (organizationUnitIds.Count == 0)
        {
            return new List<Guid>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        return await _asyncExecuter.ToListAsync(
            userQuery
                .Where(u => u.IsActive)
                .Where(u => u.OrganizationUnits.Any(ou => organizationUnitIds.Contains(ou.OrganizationUnitId)))
                .Select(u => u.Id)
                .Distinct());
    }
}
