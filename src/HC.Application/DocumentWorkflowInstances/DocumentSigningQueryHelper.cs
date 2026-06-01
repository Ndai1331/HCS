using System;
using System.Collections.Generic;
using System.Text.Json;

namespace HC.DocumentWorkflowInstances;

internal static class DocumentSigningQueryHelper
{
    public static List<Guid>? TryDeserializeCommittedStepTemplateIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json);
        }
        catch
        {
            return null;
        }
    }

    public static int? GetTotalStepsForDisplay(
        DocumentWorkflowInstance instance,
        IReadOnlyDictionary<Guid, int> legacyActiveStepCountByTemplateId)
    {
        var ids = TryDeserializeCommittedStepTemplateIds(instance.CommittedStepTemplateIdsJson);
        if (ids is { Count: > 0 })
        {
            return ids.Count;
        }

        if (legacyActiveStepCountByTemplateId.TryGetValue(instance.WorkflowTemplateId, out var count) && count > 0)
        {
            return count;
        }

        return null;
    }
}
