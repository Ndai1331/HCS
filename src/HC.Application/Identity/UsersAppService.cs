using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading.Tasks;
using HC.Positions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Data;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;

namespace HC.Identity;

[Authorize(IdentityPermissions.Users.Default)]
public class UsersAppService : HCAppService, IUsersAppService
{
    private const string PositionIdPropertyName = "PositionId";

    private readonly IRepository<IdentityUser, Guid> _identityUserRepository;
    private readonly IRepository<OrganizationUnit, Guid> _organizationUnitRepository;
    private readonly IPositionRepository _positionRepository;
    private readonly IdentityUserManager _identityUserManager;

    public UsersAppService(
        IRepository<IdentityUser, Guid> identityUserRepository,
        IRepository<OrganizationUnit, Guid> organizationUnitRepository,
        IPositionRepository positionRepository,
        IdentityUserManager identityUserManager)
    {
        _identityUserRepository = identityUserRepository;
        _organizationUnitRepository = organizationUnitRepository;
        _positionRepository = positionRepository;
        _identityUserManager = identityUserManager;
    }

    public virtual async Task<PagedResultDto<IdentityUserWithNavigationPropertiesDto>> GetListWithNavigationPropertiesAsync(GetUsersInput input)
    {
        var query = await BuildFilteredQueryAsync(input);

        var totalCount = await AsyncExecuter.CountAsync(query);

        var sorting = string.IsNullOrWhiteSpace(input.Sorting) ? nameof(IdentityUser.UserName) : input.Sorting;
        query = query.OrderBy(sorting);

        var users = await AsyncExecuter.ToListAsync(
            query.PageBy(input.SkipCount, input.MaxResultCount));

        var userIds = users.Select(u => u.Id).ToList();
        var rolesNameByUserId = await GetRolesNameByUserIdsAsync(userIds);
        var organizationUnitsNameByUserId = await GetOrganizationUnitsNameByUserIdsAsync(userIds);
        var positionNameById = await GetPositionNameByIdsAsync(users);

        var items = users.Select(user =>
        {
            var positionId = user.GetProperty<Guid?>(PositionIdPropertyName);
            return new IdentityUserWithNavigationPropertiesDto
            {
                User = ObjectMapper.Map<IdentityUser, IdentityUserDto>(user),
                RolesName = rolesNameByUserId.GetValueOrDefault(user.Id, string.Empty),
                OrganizationUnitsName = organizationUnitsNameByUserId.GetValueOrDefault(user.Id, string.Empty),
                PositionId = positionId,
                PositionName = positionId.HasValue && positionId.Value != Guid.Empty
                    ? positionNameById.GetValueOrDefault(positionId.Value)
                    : null
            };
        }).ToList();

        return new PagedResultDto<IdentityUserWithNavigationPropertiesDto>(totalCount, items);
    }

    private async Task<IQueryable<IdentityUser>> BuildFilteredQueryAsync(GetUsersInput input)
    {
        var query = await _identityUserRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.FilterText))
        {
            // Search by full name in the format: (surname + " " + name).ToLower().Trim()
            var filterText = input.FilterText.Trim().ToLower();
            query = query.Where(u =>
                u.UserName.ToLower().Contains(filterText) ||
                (u.Email != null && u.Email.ToLower().Contains(filterText)) ||
                (u.PhoneNumber != null && u.PhoneNumber.Contains(filterText)) ||
                (((u.Surname ?? "") + " " + (u.Name ?? "")).Trim().ToLower()).Contains(filterText));
        }

        if (!string.IsNullOrWhiteSpace(input.UserName))
        {
            var userName = input.UserName.Trim();
            query = query.Where(u => u.UserName.Contains(userName));
        }

        if (!string.IsNullOrWhiteSpace(input.PhoneNumber))
        {
            var phoneNumber = input.PhoneNumber.Trim();
            query = query.Where(u => u.PhoneNumber != null && u.PhoneNumber.Contains(phoneNumber));
        }

        if (!string.IsNullOrWhiteSpace(input.FullName))
        {
            var fullName = input.FullName.Trim();
            query = query.Where(u =>
                (u.Name != null && u.Name.Contains(fullName)) ||
                (u.Surname != null && u.Surname.Contains(fullName)) ||
                (u.UserName != null && u.UserName.Contains(fullName)));
        }

        if (input.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == input.IsActive.Value);
        }

        if (input.OrganizationUnitId.HasValue && input.OrganizationUnitId.Value != Guid.Empty)
        {
            var organizationUnitId = input.OrganizationUnitId.Value;
            query = query.Where(u => u.OrganizationUnits.Any(ou => ou.OrganizationUnitId == organizationUnitId));
        }

        if (input.RoleId.HasValue && input.RoleId.Value != Guid.Empty)
        {
            var roleId = input.RoleId.Value;
            query = query.Where(u => u.Roles.Any(r => r.RoleId == roleId));
        }

        return query;
    }

    private async Task<Dictionary<Guid, string>> GetRolesNameByUserIdsAsync(List<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        // Use IdentityUserManager so list roles match detail popup (GetRolesAsync).
        var result = new Dictionary<Guid, string>();
        foreach (var userId in userIds)
        {
            var user = await _identityUserManager.GetByIdAsync(userId);
            if (user == null)
            {
                continue;
            }

            var roleNames = await _identityUserManager.GetRolesAsync(user);
            result[userId] = string.Join(
                ", ",
                roleNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
        }

        return result;
    }

    private async Task<Dictionary<Guid, string>> GetOrganizationUnitsNameByUserIdsAsync(List<Guid> userIds)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var userQuery = await _identityUserRepository.GetQueryableAsync();
        var ouQuery = await _organizationUnitRepository.GetQueryableAsync();

        var userOrganizationUnits = await AsyncExecuter.ToListAsync(
            from user in userQuery
            where userIds.Contains(user.Id)
            from userOu in user.OrganizationUnits
            join ou in ouQuery on userOu.OrganizationUnitId equals ou.Id
            select new { UserId = user.Id, OuName = ou.DisplayName });

        return userOrganizationUnits
            .GroupBy(x => x.UserId)
            .ToDictionary(
                g => g.Key,
                g => string.Join(
                    ", ",
                    g.Select(x => x.OuName).Where(name => !string.IsNullOrWhiteSpace(name)).Distinct()));
    }

    private async Task<Dictionary<Guid, string>> GetPositionNameByIdsAsync(List<IdentityUser> users)
    {
        var positionIds = users
            .Select(u => u.GetProperty<Guid?>(PositionIdPropertyName))
            .Where(id => id.HasValue && id.Value != Guid.Empty)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        if (positionIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var positions = await _positionRepository.GetListAsync(p => positionIds.Contains(p.Id));
        return positions.ToDictionary(
            p => p.Id,
            p => !string.IsNullOrWhiteSpace(p.Name) ? p.Name! : p.Code ?? string.Empty);
    }
}
