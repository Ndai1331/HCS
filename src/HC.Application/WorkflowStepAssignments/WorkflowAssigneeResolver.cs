using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Microsoft.Extensions.Localization;
using HC.Localization;
using Volo.Abp.Linq;

namespace HC.WorkflowStepAssignments;

public class WorkflowAssigneeResolver : IWorkflowAssigneeResolver, ITransientDependency
{
    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IOrganizationUnitRepository _organizationUnitRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IStringLocalizer<HCResource> _localizer;

    public WorkflowAssigneeResolver(
        IRepository<IdentityUser, Guid> identityUserRepository,
        IOrganizationUnitRepository organizationUnitRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IStringLocalizer<HCResource> localizer)
    {
        _identityUserRepository = identityUserRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _asyncExecuter = asyncExecuter;
        _localizer = localizer;
    }

    public async Task<Guid?> GetSubmitterPrimaryOrganizationUnitIdAsync(Guid submitterUserId)
    {
        var userQuery = await _identityUserRepository.GetQueryableAsync();
        return await _asyncExecuter.FirstOrDefaultAsync(
            userQuery
                .Where(u => u.Id == submitterUserId)
                .SelectMany(u => u.OrganizationUnits)
                .OrderBy(ou => ou.CreationTime)
                .Select(ou => (Guid?)ou.OrganizationUnitId));
    }

    public async Task<List<WorkflowStepUserDto>> ResolveCandidatesByRoleAsync(Guid roleId, Guid submitterUserId, bool isPrimary = false)
    {
        var primaryOuId = await GetSubmitterPrimaryOrganizationUnitIdAsync(submitterUserId);
        if (!primaryOuId.HasValue)
        {
            throw new UserFriendlyException(_localizer["SubmitterHasNoOrganizationUnit"]);
        }

        var ouChain = await GetOrganizationUnitChainAsync(primaryOuId.Value);
        var ouScopeById = ouChain.ToDictionary(x => x.OrganizationUnitId);
        var ouIds = ouScopeById.Keys.ToList();

        if (!ouIds.Any())
        {
            return new List<WorkflowStepUserDto>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();

        // Project in SQL — do not read user.OrganizationUnits after ToList (navigations are not loaded).
        var candidateRows = await _asyncExecuter.ToListAsync(
            from user in userQuery
            where user.IsActive
            where user.Roles.Any(r => r.RoleId == roleId)
            from userOu in user.OrganizationUnits
            where ouIds.Contains(userOu.OrganizationUnitId)
            select new
            {
                user.Id,
                user.UserName,
                user.Surname,
                user.Name,
                userOu.OrganizationUnitId
            });

        var resultByUserId = new Dictionary<Guid, WorkflowStepUserDto>();

        foreach (var row in candidateRows)
        {
            if (!ouScopeById.TryGetValue(row.OrganizationUnitId, out var scope))
            {
                continue;
            }

            if (resultByUserId.TryGetValue(row.Id, out var existing)
                && existing.OrganizationUnitDepth <= scope.Depth)
            {
                continue;
            }

            resultByUserId[row.Id] = new WorkflowStepUserDto
            {
                UserId = row.Id,
                UserName = row.UserName ?? "Unknown",
                FullName = $"{row.Surname} {row.Name}".Trim(),
                IsPrimary = isPrimary,
                OrganizationUnitId = scope.OrganizationUnitId,
                OrganizationUnitName = scope.DisplayName,
                IsFromParentOrganizationUnit = scope.Depth > 0,
                OrganizationUnitDepth = scope.Depth
            };
        }

        return resultByUserId.Values
            .OrderBy(x => x.OrganizationUnitDepth)
            .ThenBy(x => x.FullName)
            .ToList();
    }

    private async Task<List<OrganizationUnitScope>> GetOrganizationUnitChainAsync(Guid startOrganizationUnitId)
    {
        var chain = new List<OrganizationUnitScope>();
        var currentId = startOrganizationUnitId;
        var depth = 0;
        var visited = new HashSet<Guid>();

        while (currentId != Guid.Empty && visited.Add(currentId))
        {
            var ou = await _organizationUnitRepository.GetAsync(currentId);
            chain.Add(new OrganizationUnitScope
            {
                OrganizationUnitId = ou.Id,
                DisplayName = ou.DisplayName ?? string.Empty,
                Depth = depth
            });

            if (!ou.ParentId.HasValue)
            {
                break;
            }

            currentId = ou.ParentId.Value;
            depth++;
        }

        return chain;
    }

    private sealed class OrganizationUnitScope
    {
        public Guid OrganizationUnitId { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public int Depth { get; init; }
    }
}
