using System;
using System.Collections.Generic;
using System.Linq;
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
        var unlocked = WorkflowSubmissionHelper.GetUnlockedViewStepTemplateIds(instance);
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            if (!unlocked.Contains(steps[index].StepId))
            {
                unlocked.Add(steps[index].StepId);
            }

            index++;
        }

        if (unlocked.Count > 0)
        {
            WorkflowSubmissionHelper.SetUnlockedViewStepTemplateIds(instance, unlocked);
        }

        return index;
    }

    public static int AdvanceThroughViewSteps(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepTemplate> steps,
        int startIndex)
    {
        var index = startIndex;
        var unlocked = WorkflowSubmissionHelper.GetUnlockedViewStepTemplateIds(instance);
        while (index < steps.Count && IsViewStep(steps[index].Type))
        {
            if (!unlocked.Contains(steps[index].Id))
            {
                unlocked.Add(steps[index].Id);
            }

            index++;
        }

        if (unlocked.Count > 0)
        {
            WorkflowSubmissionHelper.SetUnlockedViewStepTemplateIds(instance, unlocked);
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

    /// <summary>
    /// 1-based index among SIGN/PROCESS steps only (for Word/PDF placeholders &lt;&lt;SignNN&gt;&gt;), ordered by template Order.
    /// </summary>
    public static int GetSigningPlaceholderIndex(
        IReadOnlyList<WorkflowStepTemplate> committedStepsOrdered,
        Guid stepTemplateId)
    {
        if (!TryGetSigningPlaceholderIndex(committedStepsOrdered, stepTemplateId, out var signingIndex))
        {
            throw new InvalidOperationException(
                $"Step template {stepTemplateId} is not a blocking SIGN/PROCESS step in the committed workflow.");
        }

        return signingIndex;
    }

    /// <summary>
    /// 1-based index among SIGN/PROCESS steps only (for Word/PDF placeholders &lt;&lt;SignNN&gt;&gt;), ordered by template Order.
    /// </summary>
    public static int GetSigningPlaceholderIndex(
        IReadOnlyList<WorkflowStepDetailDto> committedStepsOrdered,
        Guid stepTemplateId)
    {
        if (!TryGetSigningPlaceholderIndex(committedStepsOrdered, stepTemplateId, out var signingIndex))
        {
            throw new InvalidOperationException(
                $"Step template {stepTemplateId} is not a blocking SIGN/PROCESS step in the committed workflow.");
        }

        return signingIndex;
    }

    public static bool TryGetSigningPlaceholderIndex(
        IReadOnlyList<WorkflowStepTemplate> committedStepsOrdered,
        Guid stepTemplateId,
        out int signingIndex)
    {
        var blocking = committedStepsOrdered
            .Where(s => IsBlockingStep(s.Type))
            .OrderBy(s => s.Order)
            .ToList();
        var index = blocking.FindIndex(s => s.Id == stepTemplateId);
        if (index < 0)
        {
            signingIndex = 0;
            return false;
        }

        signingIndex = index + 1;
        return true;
    }

    public static bool TryGetSigningPlaceholderIndex(
        IReadOnlyList<WorkflowStepDetailDto> committedStepsOrdered,
        Guid stepTemplateId,
        out int signingIndex)
    {
        var blocking = committedStepsOrdered
            .Where(s => IsBlockingStep(s.Type))
            .OrderBy(s => s.Order)
            .ToList();
        var index = blocking.FindIndex(s => s.StepId == stepTemplateId);
        if (index < 0)
        {
            signingIndex = 0;
            return false;
        }

        signingIndex = index + 1;
        return true;
    }
}
