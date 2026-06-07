using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HC.DocumentWorkflowInstances;

public class WorkflowViewScopeData
{
    public List<Guid> OrganizationUnitIds { get; set; } = new();

    public List<Guid> UserIds { get; set; } = new();

    public bool HasAnySelection()
    {
        return OrganizationUnitIds.Any(x => x != Guid.Empty)
               || UserIds.Any(x => x != Guid.Empty);
    }
}

public static class WorkflowViewScopeHelper
{
    public const string ViewStepScopesExtraPropertyName = "ViewStepScopesJson";

    public static Dictionary<Guid, WorkflowViewScopeData> GetViewStepScopes(
        string? viewStepScopesJson,
        IDictionary<string, object?>? extraProperties = null)
    {
        var json = viewStepScopesJson;
        if (string.IsNullOrWhiteSpace(json)
            && extraProperties != null
            && extraProperties.TryGetValue(ViewStepScopesExtraPropertyName, out var raw)
            && raw != null)
        {
            json = raw switch
            {
                string str => str,
                JsonElement element when element.ValueKind == JsonValueKind.String => element.GetString(),
                JsonElement element => element.GetRawText(),
                _ => raw.ToString()
            };
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<Guid, WorkflowViewScopeData>();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<Guid, WorkflowViewScopeData>>(json);
            if (parsed == null)
            {
                return new Dictionary<Guid, WorkflowViewScopeData>();
            }

            foreach (var entry in parsed.Values)
            {
                entry.OrganizationUnitIds = entry.OrganizationUnitIds
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToList();
                entry.UserIds = entry.UserIds
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToList();
            }

            return parsed;
        }
        catch
        {
            return new Dictionary<Guid, WorkflowViewScopeData>();
        }
    }

    public static string? SerializeViewStepScopes(IReadOnlyDictionary<Guid, WorkflowViewScopeData> scopes)
    {
        if (scopes == null || scopes.Count == 0)
        {
            return null;
        }

        var normalized = scopes
            .Where(x => x.Key != Guid.Empty && x.Value.HasAnySelection())
            .ToDictionary(x => x.Key, x => x.Value);

        return normalized.Count == 0 ? null : JsonSerializer.Serialize(normalized);
    }

    public static WorkflowViewScopeData? GetScopeForStep(
        string? viewStepScopesJson,
        Guid stepId,
        IDictionary<string, object?>? extraProperties = null)
    {
        var map = GetViewStepScopes(viewStepScopesJson, extraProperties);
        return map.TryGetValue(stepId, out var scope) ? scope : null;
    }
}
