using System;

namespace HC.Dashboard;

/// <summary>
/// Query for the home dashboard bundle. The authenticated user is always taken from
/// <c>ICurrentUser</c> — never from this DTO.
/// </summary>
public class GetHomeDashboardBundleInput
{
    /// <summary>Inclusive start date (calendar day). When null, defaults to 60 days before today.</summary>
    public DateTime? StartDate { get; set; }

    /// <summary>Inclusive end date (calendar day). When null, defaults to today.</summary>
    public DateTime? EndDate { get; set; }

    /// <summary>Optional UI culture for localized notification text (e.g. vi, en).</summary>
    public string? Culture { get; set; }
}
