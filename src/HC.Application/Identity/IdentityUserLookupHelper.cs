using System.Linq;
using Volo.Abp.Identity;

namespace HC.Identity;

/// <summary>
/// Shared query helpers for building IdentityUser lookups (user pickers).
/// </summary>
public static class IdentityUserLookupHelper
{
    /// <summary>
    /// Applies a full-name search using the convention:
    /// (surname + " " + name).Trim().ToLower().Contains(filter).
    /// </summary>
    public static IQueryable<IdentityUser> ApplyFullNameFilter(IQueryable<IdentityUser> query, string? filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return query;
        }

        var filterLower = filter.Trim().ToLower();
        return query.Where(u =>
            ((u.Surname ?? "") + " " + (u.Name ?? "")).Trim().ToLower().Contains(filterLower));
    }
}
