using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HC.Projects;
using HC.ProjectTasks;

namespace HC.Dashboard;

/// <summary>
/// Aggregated dashboard counts loaded via repositories (avoids chaining through multiple AppServices).
/// </summary>
public class HomeDashboardQueryResult
{
    public long TotalProjectsCount { get; set; }

    public long FilteredProjectsCount { get; set; }

    public List<ProjectWithNavigationProperties> FilteredProjects { get; set; } = new();

    public long TotalTasksCount { get; set; }

    public Dictionary<string, int> TasksByStatus { get; set; } = new();

    public List<ProjectTaskWithNavigationProperties> FilteredTasks { get; set; } = new();
}

public interface IHomeDashboardQueryRepository
{
    Task<HomeDashboardQueryResult> GetProjectAndTaskSummaryAsync(
        DateTime filterStart,
        DateTime filterEndExclusive,
        int maxListItems = 200,
        CancellationToken cancellationToken = default);
}
