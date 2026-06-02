using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstances;
using Xunit;

namespace HC.Domain.Shared.Tests.DocumentWorkflowInstances;

public class WorkflowSubmissionHelperTests
{
    [Fact]
    public void SetAndGetStepSignerSelections_ShouldRoundtripSelections()
    {
        var instance = CreateInstance();
        var step1 = Guid.NewGuid();
        var step2 = Guid.NewGuid();
        var user1 = Guid.NewGuid();
        var user2 = Guid.NewGuid();

        WorkflowSubmissionHelper.SetStepSignerSelections(instance, new List<WorkflowStepSignerSelectionDto>
        {
            new() { StepId = step1, SelectedUserId = user1 },
            new() { StepId = step2, SelectedUserId = user2 }
        });

        var map = WorkflowSubmissionHelper.GetStepSignerSelections(instance);

        Assert.Equal(2, map.Count);
        Assert.Equal(user1, map[step1]);
        Assert.Equal(user2, map[step2]);
    }

    [Fact]
    public void SetSelectedSignerForStep_ShouldUpdateExistingSelection()
    {
        var instance = CreateInstance();
        var step = Guid.NewGuid();
        var oldUser = Guid.NewGuid();
        var newUser = Guid.NewGuid();

        WorkflowSubmissionHelper.SetStepSignerSelections(instance, new List<WorkflowStepSignerSelectionDto>
        {
            new() { StepId = step, SelectedUserId = oldUser }
        });

        WorkflowSubmissionHelper.SetSelectedSignerForStep(instance, step, newUser);

        var selected = WorkflowSubmissionHelper.GetSelectedSignerForStep(instance, step);
        Assert.Equal(newUser, selected);
    }

    private static DocumentWorkflowInstance CreateInstance()
    {
        return new DocumentWorkflowInstance(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
            DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1));
    }
}
