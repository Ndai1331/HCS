using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using HC.Localization;
using HC.WorkflowStepAssignments;
using Microsoft.Extensions.Localization;
using NSubstitute;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Identity;
using Volo.Abp.Linq;
using Xunit;

namespace HC.Domain.Shared.Tests.WorkflowStepAssignments;

public class WorkflowAssigneeResolverOuScopeTests
{
    private readonly Guid _parentOuId = Guid.NewGuid();
    private readonly Guid _childOuId = Guid.NewGuid();
    private readonly Guid _grandchildOuId = Guid.NewGuid();
    private readonly Guid _siblingOuId = Guid.NewGuid();
    private readonly Guid _secondBranchOuId = Guid.NewGuid();
    private readonly Guid _secondBranchChildOuId = Guid.NewGuid();

    [Fact]
    public async Task GetOrganizationUnitAndDescendantsScopeForUserAsync_UserInChildOu_ExcludesParent()
    {
        var userId = Guid.NewGuid();
        var resolver = CreateResolver(
            users: [CreateUser(userId, _childOuId)],
            organizationUnits: CreateStandardOuTree());

        var scope = await resolver.GetOrganizationUnitAndDescendantsScopeForUserAsync(userId);

        Assert.Contains(_childOuId, scope);
        Assert.Contains(_grandchildOuId, scope);
        Assert.DoesNotContain(_parentOuId, scope);
        Assert.DoesNotContain(_siblingOuId, scope);
    }

    [Fact]
    public async Task GetOrganizationUnitAndDescendantsScopeForUserAsync_UserInParentOu_IncludesChildrenOnly()
    {
        var userId = Guid.NewGuid();
        var resolver = CreateResolver(
            users: [CreateUser(userId, _parentOuId)],
            organizationUnits: CreateStandardOuTree());

        var scope = await resolver.GetOrganizationUnitAndDescendantsScopeForUserAsync(userId);

        Assert.Contains(_parentOuId, scope);
        Assert.Contains(_childOuId, scope);
        Assert.Contains(_grandchildOuId, scope);
        Assert.Contains(_siblingOuId, scope);
        Assert.Equal(4, scope.Count);
    }

    [Fact]
    public async Task GetOrganizationUnitAndDescendantsScopeForUserAsync_UserInTwoOus_UnionsBothBranches()
    {
        var userId = Guid.NewGuid();
        var ous = CreateStandardOuTree();
        ous[_secondBranchOuId] = CreateOrganizationUnit(_secondBranchOuId, "Branch2", _parentOuId, "00001.00005");
        ous[_secondBranchChildOuId] = CreateOrganizationUnit(
            _secondBranchChildOuId,
            "Branch2Child",
            _secondBranchOuId,
            "00001.00005.00006");

        var resolver = CreateResolver(
            users: [CreateUser(userId, _childOuId, _secondBranchOuId)],
            organizationUnits: ous);

        var scope = await resolver.GetOrganizationUnitAndDescendantsScopeForUserAsync(userId);

        Assert.Contains(_childOuId, scope);
        Assert.Contains(_grandchildOuId, scope);
        Assert.Contains(_secondBranchOuId, scope);
        Assert.Contains(_secondBranchChildOuId, scope);
        Assert.DoesNotContain(_parentOuId, scope);
        Assert.DoesNotContain(_siblingOuId, scope);
    }

    [Fact]
    public async Task GetOrganizationUnitAndDescendantsScopeForUserAsync_UserWithNoOu_ReturnsEmpty()
    {
        var userId = Guid.NewGuid();
        var resolver = CreateResolver(
            users: [CreateUser(userId)],
            organizationUnits: CreateStandardOuTree());

        var scope = await resolver.GetOrganizationUnitAndDescendantsScopeForUserAsync(userId);

        Assert.Empty(scope);
    }

    [Fact]
    public async Task GetOrganizationUnitScopeWithDescendantsForUserAsync_StillIncludesAncestors()
    {
        var userId = Guid.NewGuid();
        var resolver = CreateResolver(
            users: [CreateUser(userId, _childOuId)],
            organizationUnits: CreateStandardOuTree());

        var scope = await resolver.GetOrganizationUnitScopeWithDescendantsForUserAsync(userId);

        Assert.Contains(_parentOuId, scope);
        Assert.Contains(_childOuId, scope);
        Assert.Contains(_grandchildOuId, scope);
    }

    private static IdentityUser CreateUser(Guid userId, params Guid[] organizationUnitIds)
    {
        var user = new IdentityUser(userId, $"user-{userId:N}", $"user-{userId:N}@test.com");
        foreach (var ouId in organizationUnitIds)
        {
            user.AddOrganizationUnit(ouId);
        }

        return user;
    }

    private Dictionary<Guid, OrganizationUnit> CreateStandardOuTree()
    {
        return new Dictionary<Guid, OrganizationUnit>
        {
            [_parentOuId] = CreateOrganizationUnit(_parentOuId, "Parent", null, "00001"),
            [_childOuId] = CreateOrganizationUnit(_childOuId, "Child", _parentOuId, "00001.00002"),
            [_grandchildOuId] = CreateOrganizationUnit(_grandchildOuId, "Grandchild", _childOuId, "00001.00002.00003"),
            [_siblingOuId] = CreateOrganizationUnit(_siblingOuId, "Sibling", _parentOuId, "00001.00004")
        };
    }

    private static OrganizationUnit CreateOrganizationUnit(Guid id, string displayName, Guid? parentId, string code)
    {
        var ou = new OrganizationUnit(id, displayName, parentId);
        SetOrganizationUnitCode(ou, code);
        return ou;
    }

    private static void SetOrganizationUnitCode(OrganizationUnit ou, string code)
    {
        typeof(OrganizationUnit).GetProperty(
            nameof(OrganizationUnit.Code),
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(ou, code);
    }

    private static WorkflowAssigneeResolver CreateResolver(
        IReadOnlyList<IdentityUser> users,
        IReadOnlyDictionary<Guid, OrganizationUnit> organizationUnits)
    {
        var userRepository = Substitute.For<IRepository<IdentityUser, Guid>>();
        userRepository.GetQueryableAsync().Returns(users.AsQueryable());

        var organizationUnitRepository = Substitute.For<IOrganizationUnitRepository>();
        foreach (var (ouId, ou) in organizationUnits)
        {
            organizationUnitRepository.FindAsync(ouId).Returns(ou);
            organizationUnitRepository.GetAsync(ouId).Returns(ou);
        }

        organizationUnitRepository
            .GetAllChildrenWithParentCodeAsync(Arg.Any<string>(), Arg.Any<Guid>())
            .Returns(callInfo =>
            {
                var parentId = callInfo.ArgAt<Guid>(1);
                return organizationUnits.Values
                    .Where(ou => IsDescendantOf(ou, parentId, organizationUnits))
                    .ToList();
            });

        var asyncExecuter = Substitute.For<IAsyncQueryableExecuter>();
        asyncExecuter
            .ToListAsync(Arg.Any<IQueryable<Guid>>())
            .Returns(callInfo => Task.FromResult(callInfo.Arg<IQueryable<Guid>>().ToList()));

        var localizer = Substitute.For<IStringLocalizer<HCResource>>();
        localizer[Arg.Any<string>()].Returns(callInfo => new LocalizedString(callInfo.Arg<string>(), callInfo.Arg<string>()));

        return new WorkflowAssigneeResolver(
            userRepository,
            organizationUnitRepository,
            asyncExecuter,
            localizer);
    }

    private static bool IsDescendantOf(
        OrganizationUnit candidate,
        Guid ancestorId,
        IReadOnlyDictionary<Guid, OrganizationUnit> organizationUnits)
    {
        if (candidate.Id == ancestorId)
        {
            return false;
        }

        var currentParentId = candidate.ParentId;
        while (currentParentId.HasValue)
        {
            if (currentParentId.Value == ancestorId)
            {
                return true;
            }

            if (!organizationUnits.TryGetValue(currentParentId.Value, out var parent))
            {
                return false;
            }

            currentParentId = parent.ParentId;
        }

        return false;
    }
}
