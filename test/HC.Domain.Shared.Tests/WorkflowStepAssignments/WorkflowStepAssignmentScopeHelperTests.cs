using System;
using System.Collections.Generic;
using HC.WorkflowStepAssignments;
using Xunit;

namespace HC.Domain.Shared.Tests.WorkflowStepAssignments;

public class WorkflowStepAssignmentScopeHelperTests
{
    [Fact]
    public void BuildScopeFields_ShouldSerializeDistinctIds()
    {
        var ou1 = Guid.NewGuid();
        var ou2 = Guid.NewGuid();
        var user1 = Guid.NewGuid();

        var (ouJson, userJson, legacyUserId) = WorkflowStepAssignmentScopeHelper.BuildScopeFields(
            new List<Guid> { ou1, ou1, ou2 },
            new List<Guid> { user1 });

        var ouIds = WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(ouJson);
        var userIds = WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(userJson, legacyUserId);

        Assert.Equal(2, ouIds.Count);
        Assert.Contains(ou1, ouIds);
        Assert.Contains(ou2, ouIds);
        Assert.Single(userIds);
        Assert.Equal(user1, userIds[0]);
        Assert.Equal(user1, legacyUserId);
    }

    [Fact]
    public void HasResolvableScope_ShouldRequireOuOrUserForScopedAssignee()
    {
        var hasScope = WorkflowStepAssignmentScopeHelper.HasResolvableScope(
            WorkflowStepAssigneeTypeNames.ScopedAssignee,
            new List<Guid> { Guid.NewGuid() },
            new List<Guid>(),
            null);

        Assert.True(hasScope);

        var missingScope = WorkflowStepAssignmentScopeHelper.HasResolvableScope(
            WorkflowStepAssigneeTypeNames.ScopedAssignee,
            new List<Guid>(),
            new List<Guid>(),
            Guid.NewGuid());

        Assert.False(missingScope);
    }

    [Fact]
    public void HasResolvableScope_ShouldAcceptOuOnlyForViewCatalogScopedAssignee()
    {
        var hasOuOnly = WorkflowStepAssignmentScopeHelper.HasResolvableScope(
            WorkflowStepAssigneeTypeNames.ScopedAssignee,
            new List<Guid> { Guid.NewGuid() },
            new List<Guid>(),
            null);

        Assert.True(hasOuOnly);
    }

    [Fact]
    public void NormalizeIds_ShouldExcludeEmptyGuid()
    {
        var empty = Guid.Empty;
        var valid = Guid.NewGuid();
        var normalized = WorkflowStepAssignmentScopeHelper.NormalizeIds(new List<Guid> { empty, valid, empty });

        Assert.Single(normalized);
        Assert.Equal(valid, normalized[0]);
    }
}
