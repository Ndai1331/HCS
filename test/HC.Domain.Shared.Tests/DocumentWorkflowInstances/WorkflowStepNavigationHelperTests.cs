using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstances;
using HC.WorkflowStepTemplates;
using Xunit;

namespace HC.DocumentWorkflowInstances;

public class WorkflowStepNavigationHelperTests
{
    [Fact]
    public void GetSigningPlaceholderIndex_ShouldIgnoreViewStepsAndUseBlockingOrder()
    {
        var viewId = Guid.NewGuid();
        var sign1Id = Guid.NewGuid();
        var sign2Id = Guid.NewGuid();
        var steps = new List<WorkflowStepTemplate>
        {
            CreateStep(viewId, 1, nameof(WorkflowStepType.VIEW)),
            CreateStep(sign1Id, 2, nameof(WorkflowStepType.SIGN)),
            CreateStep(sign2Id, 3, nameof(WorkflowStepType.SIGN)),
        };

        Assert.Equal(1, WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(steps, sign1Id));
        Assert.Equal(2, WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(steps, sign2Id));
    }

    [Fact]
    public void GetSigningPlaceholderIndex_FromDetailDto_ShouldMatchTemplateOverload()
    {
        var viewId = Guid.NewGuid();
        var signId = Guid.NewGuid();
        var templateSteps = new List<WorkflowStepTemplate>
        {
            CreateStep(viewId, 1, nameof(WorkflowStepType.VIEW)),
            CreateStep(signId, 2, nameof(WorkflowStepType.PROCESS)),
        };
        var detailSteps = new List<WorkflowStepDetailDto>
        {
            new() { StepId = viewId, Order = 1, Type = nameof(WorkflowStepType.VIEW) },
            new() { StepId = signId, Order = 2, Type = nameof(WorkflowStepType.PROCESS) },
        };

        Assert.Equal(
            WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(templateSteps, signId),
            WorkflowStepNavigationHelper.GetSigningPlaceholderIndex(detailSteps, signId));
    }

    [Fact]
    public void TryGetSigningPlaceholderIndex_ShouldReturnFalseForViewStep()
    {
        var viewId = Guid.NewGuid();
        var steps = new List<WorkflowStepTemplate>
        {
            CreateStep(viewId, 1, nameof(WorkflowStepType.VIEW)),
        };

        Assert.False(WorkflowStepNavigationHelper.TryGetSigningPlaceholderIndex(steps, viewId, out _));
    }

    private static WorkflowStepTemplate CreateStep(Guid id, int order, string type)
    {
        return new WorkflowStepTemplate(
            id,
            Guid.NewGuid(),
            order,
            $"Step-{order}",
            type,
            allowReturn: false,
            isActive: true,
            sLADays: 0);
    }
}
