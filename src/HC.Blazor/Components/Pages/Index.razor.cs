using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.CalendarEvents;
using HC.Documents;
using HC.NotificationReceivers;
using HC.Notifications;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using Humanizer;
using Volo.Abp.BlobStoring;
using Blazorise;
using Volo.Abp.AspNetCore.Components.BlockUi;

namespace HC.Blazor.Components.Pages;

public partial class Index
{
    [Inject]
    protected NavigationManager Navigation { get; set; } = default!;

    [Inject] private IProjectsAppService ProjectsAppService { get; set; } = default!;
    [Inject] private IProjectTasksAppService ProjectTasksAppService { get; set; } = default!;
    [Inject] private IProjectTaskAssignmentsAppService ProjectTaskAssignmentsAppService { get; set; } = default!;
    [Inject] private IProjectTaskDocumentsAppService ProjectTaskDocumentsAppService { get; set; } = default!;
    [Inject] private ICalendarEventsAppService CalendarEventsAppService { get; set; } = default!;
    [Inject] private IDocumentsAppService DocumentsAppService { get; set; } = default!;
    [Inject] private INotificationReceiversAppService NotificationReceiversAppService { get; set; } = default!;
    [Inject] private IDocumentAssignmentsAppService DocumentAssignmentsAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private IBlockUiService BlockUiService { get; set; } = default!;

    // Active Projects data
    private List<ProjectWithNavigationPropertiesDto> ActiveProjectsList { get; set; } = new();
    private int TotalProjectsCount { get; set; }
    private int ActiveProjectsCount { get; set; }

    // Tasks data
    private Dictionary<string, int> TasksByStatus { get; set; } = new();
    private int TotalTasksCount { get; set; }

    // Calendar events data
    private List<CalendarEventDto> CalendarEventsList { get; set; } = new();

    // Documents data - Loaded from DocumentAssignment for the current user
    private List<DocumentAssignmentWithNavigationPropertiesDto> RecentDocumentsList { get; set; } = new();

    // Notifications data
    private List<NotificationReceiverWithNavigationPropertiesDto> RecentNotificationsList { get; set; } = new();

    // My Tasks data - Tasks from last 60 days
    private List<ProjectTaskWithNavigationPropertiesDto> MyTasksList { get; set; } = new();

    // Task detail modal
    private Modal TaskDetailModal { get; set; } = default!;
    private ProjectTaskWithNavigationPropertiesDto? SelectedTask { get; set; }
    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> SelectedTaskAssignments { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> SelectedTaskDocuments { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private string SelectedTab { get; set; } = "general";

    // PDF viewer for task documents
    private string? PdfFileUrl { get; set; }
    private bool IsPdfFile { get; set; }
    private Modal PdfViewerModal { get; set; } = default!;
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    private bool IsLoading { get; set; } = true;
    private string LastNotificationTimeAgo { get; set; } = string.Empty;
    private string LastDocumentTimeAgo { get; set; } = string.Empty;
    private int TotalEvents { get; set; } = 0;

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
                LoadCalendarEventsAsync(),
                LoadRecentDocumentsAsync(),
                LoadRecentNotificationsAsync(),
                LoadMyTasksAsync()
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
                EndTimeMin = now,
                StartTimeMax = now.AddDays(7),
                MaxResultCount = 1000,
                SkipCount = 0,
                Sorting = "StartTime"
            });

            CalendarEventsList = result.Items.ToList();
            TotalEvents = (int)result.TotalCount;
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
            // Load documents assigned to current user from DocumentAssignment
            var result = await DocumentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
            {
                ReceiverUserId = CurrentUser.Id,
                MaxResultCount = 10,
                SkipCount = 0,
                Sorting = "DocumentAssignment.CreationTime DESC"
            });

            RecentDocumentsList = result.Items.ToList();
            LastDocumentTimeAgo = result.Items.Any() ? result.Items.Last().DocumentAssignment.CreationTime.Humanize() : string.Empty;
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
            LastNotificationTimeAgo = result.Items.Any() ? result.Items.Last().NotificationReceiver.CreationTime.Humanize() : string.Empty;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadMyTasksAsync()
    {
        try
        {
            // Get tasks from last 60 days based on StartDate
            var now = DateTime.Now;
            var startDate = now.AddDays(-60);

            var result = await ProjectTasksAppService.GetListAsync(new GetProjectTasksInput
            {
                StartDateMin = startDate,
                MaxResultCount = 1000,
                SkipCount = 0,
                Sorting = "ProjectTask.StartDate DESC"
            });

            MyTasksList = result.Items.ToList();
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

    // Task detail modal methods
    private async Task OpenTaskDetailModalAsync(ProjectTaskWithNavigationPropertiesDto task)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            SelectedTask = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(task.ProjectTask.Id);
            SelectedTab = "general";

            // Load assignments
            var assignmentsResult = await ProjectTaskAssignmentsAppService.GetListAsync(new GetProjectTaskAssignmentsInput
            {
                ProjectTaskId = SelectedTask.ProjectTask.Id,
                MaxResultCount = 100,
                SkipCount = 0
            });
            SelectedTaskAssignments = assignmentsResult.Items;

            // Load documents
            var documentsResult = await ProjectTaskDocumentsAppService.GetListAsync(new GetProjectTaskDocumentsInput
            {
                ProjectTaskId = SelectedTask.ProjectTask.Id,
                MaxResultCount = 100,
                SkipCount = 0
            });
            SelectedTaskDocuments = documentsResult.Items;

            // Cache PDF info for documents
            await CacheDocumentPdfInfoAsync(SelectedTaskDocuments);
            await TaskDetailModal.Show();

        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private async Task CloseTaskDetailModalAsync()
    {
        if (TaskDetailModal != null)
        {
            await TaskDetailModal.Hide();
        }
        SelectedTask = null;
        SelectedTaskAssignments = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
        SelectedTaskDocuments = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    }

    private void OnSelectedTabChanged(string name)
    {
        SelectedTab = name;
    }

    private string GetUserDisplayName(Volo.Abp.Identity.IdentityUserDto user)
    {
        var fullName = $"{user.Name} {user.Surname}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return user.UserName ?? string.Empty;
    }

    private string GetUserInitial(Volo.Abp.Identity.IdentityUserDto user)
    {
        var name = (user.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Substring(0, 1).ToUpperInvariant();
        }

        var userName = (user.UserName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Substring(0, 1).ToUpperInvariant();
        }

        return "?";
    }

    // PDF viewer methods
    private async Task CacheDocumentPdfInfoAsync(IEnumerable<ProjectTaskDocumentWithNavigationPropertiesDto> documents)
    {
        foreach (var doc in documents)
        {
            if (doc.Document != null && !DocumentHasPdfCache.ContainsKey(doc.Document.Id))
            {
                var hasPdf = await CheckIfDocumentHasPdfFileAsync(doc.Document.Id);
                DocumentHasPdfCache[doc.Document.Id] = hasPdf;
            }
        }
    }

    private async Task<bool> CheckIfDocumentHasPdfFileAsync(Guid documentId)
    {
        try
        {
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1,
                SkipCount = 0
            });

            if (!documentFilesResult.Items.Any())
            {
                return false;
            }

            var documentFile = documentFilesResult.Items.First();
            return IsPdfFileExtension(documentFile.DocumentFile.Name) && !string.IsNullOrEmpty(documentFile.DocumentFile.Path);
        }
        catch
        {
            return false;
        }
    }

    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }

    private bool DocumentHasPdfFile(Guid documentId)
    {
        return DocumentHasPdfCache.TryGetValue(documentId, out var hasPdf) && hasPdf;
    }

    private async Task OpenPdfViewerModalForDocumentAsync(ProjectTaskDocumentWithNavigationPropertiesDto projectTaskDocument)
    {
        try
        {
            if (projectTaskDocument?.Document == null)
            {
                return;
            }

            // Get document files for this document
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = projectTaskDocument.Document.Id,
                MaxResultCount = 1,
                SkipCount = 0
            });

            if (documentFilesResult.Items.Any())
            {
                var documentFile = documentFilesResult.Items.First();

                if (!string.IsNullOrEmpty(documentFile.DocumentFile.Path))
                {
                    // Get file bytes from MinIO
                    var fileBytes = await BlobContainer.GetAllBytesAsync(documentFile.DocumentFile.Path);

                    // Create data URL for PDF
                    var base64 = Convert.ToBase64String(fileBytes);
                    PdfFileUrl = $"data:application/pdf;base64,{base64}";
                    IsPdfFile = true;

                    // Open PDF viewer modal
                    if (PdfViewerModal != null)
                    {
                        await PdfViewerModal.Show();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task ClosePdfViewerModalAsync()
    {
        if (PdfViewerModal != null)
        {
            await PdfViewerModal.Hide();
        }
        PdfFileUrl = null;
        IsPdfFile = false;
    }

    protected string GetStatusBadgeColor(string status)
    {
        return status switch
        {
            "TODO" => "secondary",
            "IN_PROGRESS" => "primary",
            "WAITING" => "warning",
            "DONE" => "success",
            "CANCELLED" => "danger",
            _ => "secondary",
        };
    }


    protected string GetPriorityText(ProjectTaskPriority priority)
    {
        return L[$"Enum:ProjectTaskPriority.{priority}"];
    }
    
    protected string GetPriorityBadgeColor(string priority)
    {
        return priority switch
        {
            "LOW" => "secondary",
            "MEDIUM" => "info",
            "HIGH" => "warning",
            "URGENT" => "danger",
            _ => "secondary",
        };
    }

    
    protected Color GetPercentBadgeColor(int progressPercent)
    {
        return progressPercent switch
        {
            < 30 => Color.Danger,
            >= 30 and < 75 => Color.Warning,
            >= 75 and < 100 => Color.Primary,
            >= 100 => Color.Success
        };
    }

    private string GetTaskPriorityBadgeClass(string priority)
    {
        return priority?.ToLower() switch
        {
            "low" => "hc-badge-secondary-subtle",
            "medium" => "hc-badge-info-subtle",
            "high" => "hc-badge-warning-subtle",
            "critical" => "hc-badge-danger-subtle",
            _ => "hc-badge-secondary-subtle"
        };
    }
}