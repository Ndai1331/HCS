using System;
using System.Collections.Generic;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowStepNavigationHelper
{
    public static bool IsViewStep(string? stepType)
    {
        return string.Equals(stepType, nameof(WorkflowStepType.VIEW), StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsBlockingStep(string? stepType)
    {
        return string.Equals(stepType, nameof(WorkflowStepType.SIGN), StringComparison.OrdinalIgnoreCase)
            || string.Equals(stepType, nameof(WorkflowStepType.PROCESS), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Unlocks consecutive VIEW steps from <paramref name="startIndex"/> and returns the index of the first blocking step,
    /// or <paramref name="steps"/> count if only VIEW steps remain.
    /// </summary>
    public static int AdvanceThroughViewSteps(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepDetailDto> steps,
        int startIndex)
    {
        var index = startIndex;
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            WorkflowSubmissionHelper.UnlockViewStep(instance, steps[index].StepId);
            index++;
        }

        return index;
    }

    public static int AdvanceThroughViewSteps(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepTemplate> steps,
        int startIndex)
    {
        var index = startIndex;
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            WorkflowSubmissionHelper.UnlockViewStep(instance, steps[index].Id);
            index++;
        }

        return index;
    }

    public static WorkflowStepDetailDto? GetFirstBlockingStepDetail(IReadOnlyList<WorkflowStepDetailDto> steps)
    {
        var index = 0;
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            index++;
        }

        return index < steps.Count ? steps[index] : null;
    }

    public static WorkflowStepTemplate? GetFirstBlockingStepTemplate(IReadOnlyList<WorkflowStepTemplate> steps)
    {
        var index = 0;
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            index++;
        }

        return index < steps.Count ? steps[index] : null;
    }

    public static IReadOnlyList<WorkflowStepDetailDto> GetBlockingStepDetails(IReadOnlyList<WorkflowStepDetailDto> steps)
    {
        var result = new List<WorkflowStepDetailDto>();
        foreach (var step in steps)
        {
            if (IsBlockingStep(step.Type))
            {
                result.Add(step);
            }
        }

        return result;
    }
}
