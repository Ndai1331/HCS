using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowSubmissionHelper
{
    internal const string StepSignerSelectionsExtraPropertyName = "WorkflowStepSignerSelectionsJson";

    public static string? SerializeCommittedStepTemplateIds(IReadOnlyList<Guid> orderedStepIds)
    {
        if (orderedStepIds == null || orderedStepIds.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(orderedStepIds);
    }

    public static bool IsWordFormatPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase);
    }

    public static void SetStepSignerSelections(
        DocumentWorkflowInstance instance,
        IReadOnlyList<WorkflowStepSignerSelectionDto>? selections)
    {
        var map = selections?
            .Where(x => x.StepId != Guid.Empty && x.SelectedUserId != Guid.Empty)
            .GroupBy(x => x.StepId)
            .ToDictionary(g => g.Key, g => g.Last().SelectedUserId)
            ?? new Dictionary<Guid, Guid>();

        if (map.Count == 0)
        {
            instance.ExtraProperties.Remove(StepSignerSelectionsExtraPropertyName);
            return;
        }

        instance.ExtraProperties[StepSignerSelectionsExtraPropertyName] = JsonSerializer.Serialize(map);
    }

    public static Dictionary<Guid, Guid> GetStepSignerSelections(DocumentWorkflowInstance instance)
    {
        if (!instance.ExtraProperties.TryGetValue(StepSignerSelectionsExtraPropertyName, out var raw) || raw == null)
        {
            return new Dictionary<Guid, Guid>();
        }

        string? json = raw switch
        {
            string str => str,
            JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
            JsonElement element => element.GetRawText(),
            _ => raw.ToString()
        };

        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<Guid, Guid>>(json)
                ?? new Dictionary<Guid, Guid>();
        }
        catch
        {
            return new Dictionary<Guid, Guid>();
        }
    }

    public static Guid? GetSelectedSignerForStep(DocumentWorkflowInstance instance, Guid stepId)
    {
        var map = GetStepSignerSelections(instance);
        return map.TryGetValue(stepId, out var selectedUserId) ? selectedUserId : null;
    }

    public static void SetSelectedSignerForStep(DocumentWorkflowInstance instance, Guid stepId, Guid selectedUserId)
    {
        var map = GetStepSignerSelections(instance);
        map[stepId] = selectedUserId;
        instance.ExtraProperties[StepSignerSelectionsExtraPropertyName] = JsonSerializer.Serialize(map);
    }
}
