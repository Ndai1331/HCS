using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Projects;
using HC.ProjectTasks;
using HC.CalendarEvents;
using HC.Documents;
using HC.NotificationReceivers;
using HC.Notifications;
using Humanizer;

namespace HC.Blazor.Components.Pages;

public partial class NewIndex
{
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject] private IProjectsAppService ProjectsAppService { get; set; } = default!;
    [Inject] private IProjectTasksAppService ProjectTasksAppService { get; set; } = default!;
    [Inject] private ICalendarEventsAppService CalendarEventsAppService { get; set; } = default!;
    [Inject] private IDocumentsAppService DocumentsAppService { get; set; } = default!;
    [Inject] private INotificationReceiversAppService NotificationReceiversAppService { get; set; } = default!;

    // Active Projects data
    private List<ProjectWithNavigationPropertiesDto> ActiveProjectsList { get; set; } = new();
    private int TotalProjectsCount { get; set; }
    private int ActiveProjectsCount { get; set; }

    // Tasks data
    private Dictionary<string, int> TasksByStatus { get; set; } = new();
    private int TotalTasksCount { get; set; }

    // Calendar events data
    private List<CalendarEventDto> CalendarEventsList { get; set; } = new();

    // Documents data
    private List<DocumentWithNavigationPropertiesDto> RecentDocumentsList { get; set; } = new();

    // Notifications data
    private List<NotificationReceiverWithNavigationPropertiesDto> RecentNotificationsList { get; set; } = new();

    private bool IsLoading { get; set; } = true;
    private string LastNotificationTimeAgo { get; set; } = string.Empty;
    private string LastDocumentTimeAgo { get; set; } = string.Empty;

    protected override async Task OnInitializedAsync()
    {
        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            // Load all data in parallel
            await Task.WhenAll(
                LoadActiveProjectsAsync(),
                LoadTasksStatisticsAsync(),
                // LoadCalendarEventsAsync(),
                LoadRecentDocumentsAsync(),
                LoadRecentNotificationsAsync()
            );

            // Generate calendar tabs after loading events
            GenerateCalendarTabs();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsLoading = false;
            StateHasChanged();
        }
    }

    private async Task LoadActiveProjectsAsync()
    {
        try
        {
            // Get all projects
            var allProjectsResult = await ProjectsAppService.GetListAsync(new GetProjectsInput
            {
                MaxResultCount = 1000,
                SkipCount = 0
            });

            TotalProjectsCount = (int)allProjectsResult.TotalCount;

            // Get all projects (not just IN_PROGRESS)
            var projectsResult = await ProjectsAppService.GetListAsync(new GetProjectsInput
            {
                MaxResultCount = 1000,
                SkipCount = 0,
                // Sorting = "Name"
            });

            ActiveProjectsList = projectsResult.Items.ToList();
            ActiveProjectsCount = (int)projectsResult.TotalCount;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadTasksStatisticsAsync()
    {
        try
        {
            // Get all tasks
            var result = await ProjectTasksAppService.GetListAsync(new GetProjectTasksInput
            {
                MaxResultCount = 1000,
                SkipCount = 0
            });

            TotalTasksCount = (int)result.TotalCount;

            // Group by status
            TasksByStatus = result.Items
                .GroupBy(t => t.ProjectTask.Status)
                .ToDictionary(g => g.Key, g => g.Count());
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadCalendarEventsAsync()
    {
        try
        {
            var now = DateTime.Now;
            var result = await CalendarEventsAppService.GetListAsync(new GetCalendarEventsInput
            {
                StartTimeMin = now,
                StartTimeMax = now.AddDays(7),
                MaxResultCount = 20,
                SkipCount = 0,
                Sorting = "StartTime"
            });

            CalendarEventsList = result.Items.ToList();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadRecentDocumentsAsync()
    {
        try
        {
            var result = await DocumentsAppService.GetListAsync(new GetDocumentsInput
            {
                CreatorId = CurrentUser.Id,
                MaxResultCount = 10,
                SkipCount = 0,
                Sorting = "Document.CreationTime DESC"
            });

            RecentDocumentsList = result.Items.ToList();
            LastDocumentTimeAgo = result.Items.Last().Document.CreationTime.Humanize();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadRecentNotificationsAsync()
    {
        try
        {
            var input = new GetNotificationReceiversInput
            {
                IdentityUserId = CurrentUser.Id,
                MaxResultCount = 10,
                SkipCount = 0,
                Sorting = "Notification.CreationTime DESC"
            };

            // Get notifications for current user
            var result = await NotificationReceiversAppService.GetListAsync(input);

            RecentNotificationsList = result.Items.ToList();
            LastNotificationTimeAgo = result.Items.Last().NotificationReceiver.CreationTime.Humanize();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private string GetLocalizedTitle(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.Title))
            return string.Empty;
        try
        {
            var localized = L[notification.Title];
            return localized?.Value ?? notification.Title;
        }
        catch
        {
            return notification.Title;
        }
    }

    private string GetLocalizedContent(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.Content))
            return string.Empty;

        var parts = notification.Content.Split('|');
        if (parts.Length > 1)
        {
            var key = parts[0];
            var parameters = parts.Skip(1).ToArray();
            try
            {
                var localizedString = L[key]?.Value;
                if (string.IsNullOrEmpty(localizedString))
                {
                    return notification.Content;
                }
                return string.Format(localizedString, parameters);
            }
            catch
            {
                return notification.Content;
            }
        }
        else
        {
            try
            {
                var localized = L[notification.Content];
                return localized?.Value ?? notification.Content;
            }
            catch
            {
                return notification.Content;
            }
        }
    }


    // Helper method to calculate project progress based on status
    private int CalculateProjectProgress(ProjectStatus status)
    {
        return status switch
        {
            ProjectStatus.PLANNING => 10,
            ProjectStatus.IN_PROGRESS => 50,
            ProjectStatus.COMPLETED => 100,
            ProjectStatus.CANCELLED => 0,
            _ => 0
        };
    }

    // Helper method to get task percentage
    private double GetTaskPercentage(string status)
    {
        if (TotalTasksCount == 0) return 0;
        if (!TasksByStatus.ContainsKey(status)) return 0;
        return Math.Round((double)TasksByStatus[status] / TotalTasksCount * 100, 1);
    }
}