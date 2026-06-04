using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstances;
using Xunit;

namespace HC.Domain.Shared.Tests.DocumentWorkflowInstances;

public class WorkflowViewScopeHelperTests
{
    [Fact]
    public void SerializeAndDeserializeViewStepScopes_ShouldRoundtrip()
    {
        var stepId = Guid.NewGuid();
        var ouId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var map = new Dictionary<Guid, WorkflowViewScopeData>
        {
            [stepId] = new WorkflowViewScopeData
            {
                OrganizationUnitIds = new List<Guid> { ouId },
                UserIds = new List<Guid> { userId }
            }
        };

        var json = WorkflowViewScopeHelper.SerializeViewStepScopes(map);
        Assert.False(string.IsNullOrWhiteSpace(json));

        var parsed = WorkflowViewScopeHelper.GetViewStepScopes(json);
        Assert.True(parsed.ContainsKey(stepId));
        Assert.Contains(ouId, parsed[stepId].OrganizationUnitIds);
        Assert.Contains(userId, parsed[stepId].UserIds);
    }
}
