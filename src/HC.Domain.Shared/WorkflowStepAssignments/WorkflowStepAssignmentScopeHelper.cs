using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HC.WorkflowStepAssignments;

public static class WorkflowStepAssignmentScopeHelper
{
    public static List<Guid> GetOrganizationUnitIds(string? organizationUnitIdsJson)
    {
        return DeserializeGuidList(organizationUnitIdsJson);
    }

    public static List<Guid> GetDefaultUserIds(string? defaultUserIdsJson, Guid? legacyDefaultUserId)
    {
        var fromJson = DeserializeGuidList(defaultUserIdsJson);
        if (fromJson.Count > 0)
        {
            return fromJson;
        }

        if (legacyDefaultUserId.HasValue && legacyDefaultUserId.Value != Guid.Empty)
        {
            return new List<Guid> { legacyDefaultUserId.Value };
        }

        return new List<Guid>();
    }

    public static string? SerializeOrganizationUnitIds(IReadOnlyList<Guid>? organizationUnitIds)
    {
        return SerializeGuidList(NormalizeIds(organizationUnitIds));
    }

    public static string? SerializeDefaultUserIds(IReadOnlyList<Guid>? defaultUserIds)
    {
        return SerializeGuidList(NormalizeIds(defaultUserIds));
    }

    public static bool HasResolvableScope(
        string? assigneeType,
        IReadOnlyList<Guid>? organizationUnitIds,
        IReadOnlyList<Guid>? defaultUserIds,
        Guid? roleId)
    {
        var type = assigneeType ?? WorkflowStepAssigneeTypeNames.SpecificUser;
        var ouIds = NormalizeIds(organizationUnitIds);
        var userIds = NormalizeIds(defaultUserIds);

        if (userIds.Count > 0 || ouIds.Count > 0)
        {
            return true;
        }

        if (type == WorkflowStepAssigneeTypeNames.RoleInSubmitterOrganizationUnit
            && roleId.HasValue
            && roleId.Value != Guid.Empty)
        {
            return true;
        }

        return false;
    }

    public static (string? OrganizationUnitIdsJson, string? DefaultUserIdsJson, Guid? LegacyDefaultUserId) BuildScopeFields(
        IReadOnlyList<Guid>? organizationUnitIds,
        IReadOnlyList<Guid>? defaultUserIds)
    {
        var ouIds = NormalizeIds(organizationUnitIds);
        var userIds = NormalizeIds(defaultUserIds);
        var legacyUserId = userIds.FirstOrDefault() is Guid first && first != Guid.Empty
            ? first
            : (Guid?)null;

        return (
            SerializeOrganizationUnitIds(ouIds),
            SerializeDefaultUserIds(userIds),
            legacyUserId);
    }

    public static List<Guid> NormalizeIds(IReadOnlyList<Guid>? ids)
    {
        if (ids == null || ids.Count == 0)
        {
            return new List<Guid>();
        }

        return ids.Where(x => x != Guid.Empty).Distinct().ToList();
    }

    private static string? SerializeGuidList(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(ids);
    }

    private static List<Guid> DeserializeGuidList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new List<Guid>();
        }

        try
        {
            return JsonSerializer.Deserialize<List<Guid>>(json)?
                       .Where(x => x != Guid.Empty)
                       .Distinct()
                       .ToList()
                   ?? new List<Guid>();
        }
        catch
        {
            return new List<Guid>();
        }
    }
}
