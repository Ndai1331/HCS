using System;
using System.Collections.Generic;
using HC.CalendarEvents;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.NotificationReceivers;
using HC.Projects;
using HC.ProjectTasks;
using Volo.Abp.Application.Dtos;

namespace HC.Dashboard;

/// <summary>
/// Single round-trip payload for the Blazor home (Index) dashboard.
/// </summary>
public class HomeDashboardBundleDto
{
    public int TotalProjectsCount { get; set; }

    /// <summary>Total count of projects matching the date filter (same as the list query).</summary>
    public int ActiveProjectsCount { get; set; }

    public List<ProjectWithNavigationPropertiesDto> ActiveProjects { get; set; } = new();

    public int TotalTasksCount { get; set; }

    /// <summary>Task counts by status string (e.g. TODO).</summary>
    public Dictionary<string, int> TasksByStatus { get; set; } = new();

    public List<ProjectTaskWithNavigationPropertiesDto> MyTasks { get; set; } = new();

    public List<CalendarEventDto> CalendarEvents { get; set; } = new();

    public List<HomeRecentDocumentItemDto> RecentDocuments { get; set; } = new();

    public WorkflowChartStatisticsDto WorkflowChartStatistics { get; set; } = new();

    public List<NotificationReceiverWithLocalizedNotificationDto> RecentNotifications { get; set; } = new();
}

/// <summary>
/// Recent document row for the home dashboard (assigned archive + personal), same shape as the Blazor page.
/// </summary>
public class HomeRecentDocumentItemDto
{
    public DocumentDto Document { get; set; } = default!;

    public DateTime Time { get; set; }
}
