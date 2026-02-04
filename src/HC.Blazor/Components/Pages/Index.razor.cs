using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.Application.Dtos;
using HC.Shared;
using HC.Blazor.Shared;
using Microsoft.AspNetCore.SignalR;
using HC.Blazor.Hubs;
using Volo.Abp.ObjectMapping;

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
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;
    [Inject] private IHubContext<NotificationHub> HubContext { get; set; } = null!;

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

    // Date Range Filter
    private IReadOnlyList<DateTime?> SelectedDateRange { get; set; } = new List<DateTime?>();
    private DateTime? FilterStartDate { get; set; }
    private DateTime? FilterEndDate { get; set; }

    // Create Project Modal
    private Modal? CreateProjectModal { get; set; }
    private ProjectCreateDto NewProject { get; set; } = new();
    private string? CreateProjectValidationErrorKey { get; set; }
    private Dictionary<string, string?> CreateProjectFieldErrors { get; set; } = new();
    private IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedDepartment { get; set; } = new();
    private DatePicker<DateTime>? NewProjectStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewProjectEndDateDatePicker { get; set; }
    private bool IsCreatingProject { get; set; }

    // Document PDF Viewer Modal
    private Modal DocumentPdfViewerModal { get; set; } = new();
    private string? DocumentPdfFileUrl { get; set; }
    private bool IsDocumentPdfFile { get; set; }

    // Notification Detail Modal
    private Modal NotificationDetailModal { get; set; } = new();
    private NotificationReceiverWithNavigationPropertiesDto? SelectedNotification { get; set; }

    // Task detail modal
    private ProjectTaskViewModal.ProjectTaskViewModal  TaskDetailModal { get; set; } = default!;

    // Create task modal
    private ProjectTaskCreateModal.ProjectTaskCreateModal CreateTaskModal { get; set; } = default!;

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
        // Initialize default date range: 60 days ago to today
        var today = DateTime.Now.Date;
        var sixtyDaysAgo = today.AddDays(-60);
        SelectedDateRange = new List<DateTime?> { sixtyDaysAgo, today };
        FilterStartDate = sixtyDaysAgo;
        FilterEndDate = today;

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

            // Get all projects with date filter
            var input = new GetProjectsInput
            {
                MaxResultCount = 1000,
                SkipCount = 0
            };

            // Apply date filter if set
            if (FilterStartDate.HasValue)
            {
                input.StartDateMin = FilterStartDate.Value.Date;
            }
            if (FilterEndDate.HasValue)
            {
                input.StartDateMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            var projectsResult = await ProjectsAppService.GetListAsync(input);

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
            // Get all tasks with date filter
            var input = new GetProjectTasksInput
            {
                MaxResultCount = 1000,
                SkipCount = 0
            };

            // Apply date filter if set
            if (FilterStartDate.HasValue)
            {
                input.StartDateMin = FilterStartDate.Value.Date;
            }
            if (FilterEndDate.HasValue)
            {
                input.StartDateMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            var result = await ProjectTasksAppService.GetListAsync(input);

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
            var input = new GetCalendarEventsInput
            {
                MaxResultCount = 1000,
                SkipCount = 0,
                Sorting = "StartTime"
            };

            // Apply date filter if set
            if (FilterStartDate.HasValue)
            {
                input.StartTimeMin = FilterStartDate.Value.Date;
            }
            else
            {
                // Default behavior: show events from now to next 7 days
                var now = DateTime.Now;
                input.StartTimeMin = now;
                input.StartTimeMax = now.AddDays(7);
            }

            if (FilterEndDate.HasValue)
            {
                input.StartTimeMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            var result = await CalendarEventsAppService.GetListAsync(input);

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
            var input = new GetDocumentAssignmentsInput
            {
                ReceiverUserId = CurrentUser.Id,
                MaxResultCount = 10,
                SkipCount = 0,
                Sorting = "DocumentAssignment.CreationTime DESC"
            };

            // Apply date filter if set
            if (FilterStartDate.HasValue)
            {
                input.AssignedAtMin = FilterStartDate.Value.Date;
            }
            if (FilterEndDate.HasValue)
            {
                input.AssignedAtMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            var result = await DocumentAssignmentsAppService.GetListAsync(input);

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
                Sorting = "NotificationReceiver.CreationTime DESC"
            };

            // Apply date filter if set
            if (FilterStartDate.HasValue)
            {
                input.CreationTimeMin = FilterStartDate.Value.Date;
            }
            if (FilterEndDate.HasValue)
            {
                input.CreationTimeMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

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
            var input = new GetProjectTasksInput
            {
                MaxResultCount = 1000,
                SkipCount = 0,
                Sorting = "ProjectTask.StartDate DESC"
            };

            // Apply date filter if set, otherwise use default 60 days
            if (FilterStartDate.HasValue)
            {
                input.StartDateMin = FilterStartDate.Value.Date;
            }
            if (FilterEndDate.HasValue)
            {
                input.StartDateMax = FilterEndDate.Value.Date.AddDays(1).AddSeconds(-1);
            }

            var result = await ProjectTasksAppService.GetListAsync(input);

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
            var fullTask = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(task.ProjectTask.Id);
            await TaskDetailModal.ShowAsync(fullTask);
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

    // -------------------------------
    // Create Project Modal Methods
    // -------------------------------

    private async Task OpenCreateProjectModalAsync()
    {
        try
        {
            // Initialize new project
            NewProject = new ProjectCreateDto
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now,
                Code = await GenerateNextProjectCodeAsync(),
                Status = ProjectStatus.PLANNING
            };
            SelectedDepartment = new List<LookupDto<Guid>>();
            CreateProjectValidationErrorKey = null;
            CreateProjectFieldErrors.Clear();

            await GetDepartmentCollectionLookupAsync();

            if (CreateProjectModal != null)
            {
                await CreateProjectModal.Show();
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseCreateProjectModalAsync()
    {
        if (CreateProjectModal != null)
        {
            await CreateProjectModal.Hide();
        }
        NewProject = new ProjectCreateDto();
        CreateProjectValidationErrorKey = null;
        CreateProjectFieldErrors.Clear();
        SelectedDepartment = new List<LookupDto<Guid>>();
    }

    private async Task CreateProjectAsync()
    {
        if (IsCreatingProject)
        {
            return;
        }

        IsCreatingProject = true;
        try
        {
            await InvokeAsync(StateHasChanged);

            if (!ValidateCreateProject())
            {
                await UiMessageService.Warn(L[CreateProjectValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }   

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            var createdProject = await ProjectsAppService.CreateAsync(NewProject);
            await UiMessageService.Success(L["SuccessfullyCreated"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));

            // Reload projects list
            await LoadActiveProjectsAsync();

            await CloseCreateProjectModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsCreatingProject = false;
            await BlockUiService.UnBlock();
            await InvokeAsync(StateHasChanged);
        }
    }

    private bool ValidateCreateProject()
    {
        // Reset error state
        CreateProjectValidationErrorKey = null;
        CreateProjectFieldErrors.Clear();

        bool isValid = true;

        // Required: Code
        if (string.IsNullOrWhiteSpace(NewProject?.Code))
        {
            CreateProjectFieldErrors["Code"] = L["CodeRequired"];
            CreateProjectValidationErrorKey = "CodeRequired";
            isValid = false;
        }

        // Required: Name
        if (string.IsNullOrWhiteSpace(NewProject?.Name))
        {
            CreateProjectFieldErrors["Name"] = L["NameRequired"];
            if (isValid)
            {
                CreateProjectValidationErrorKey = "NameRequired";
            }
            isValid = false;
        }

        return isValid;
    }

    private string? GetCreateProjectFieldError(string fieldName) => CreateProjectFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateProjectFieldError(string fieldName) => CreateProjectFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateProjectFieldErrors[fieldName]);

    private void OnDepartmentIdChanged()
    {
        if (SelectedDepartment != null && SelectedDepartment.Count > 0)
        {
            NewProject.OwnerDepartmentId = SelectedDepartment.FirstOrDefault()?.Id;
        }
        else
        {
            NewProject.OwnerDepartmentId = null;
        }
    }

    private async Task GetDepartmentCollectionLookupAsync(string? newValue = null)
    {
        DepartmentsCollection = (await ProjectsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    protected async Task<List<LookupDto<Guid>>> GetDepartmentCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        DepartmentsCollection = (await ProjectsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return DepartmentsCollection.ToList();
    }

    private async Task<string> GenerateNextProjectCodeAsync()
    {
        try
        {
            int maxNumber = 0;
            const int pageSize = 1000;
            int skipCount = 0;
            bool hasMore = true;

            // Query all projects in batches to find the highest "P" code
            while (hasMore)
            {
                var input = new GetProjectsInput
                {
                    MaxResultCount = pageSize,
                    SkipCount = skipCount,
                    Sorting = "Project.Code DESC"
                };

                var result = await ProjectsAppService.GetListAsync(input);

                if (result.Items == null || result.Items.Count == 0)
                {
                    hasMore = false;
                    break;
                }

                // Iterate through items to find the highest "P" code
                foreach (var project in result.Items)
                {
                    if (!string.IsNullOrWhiteSpace(project.Project.Code))
                    {
                        var code = project.Project.Code.Trim();

                        // Check if code starts with "P" (case-insensitive) and has numeric suffix
                        if (code.StartsWith("P", StringComparison.OrdinalIgnoreCase) && code.Length > 1)
                        {
                            // Extract number part after "P"
                            var numberPart = code.Substring(1);
                            if (int.TryParse(numberPart, out int number))
                            {
                                if (number > maxNumber)
                                {
                                    maxNumber = number;
                                }
                            }
                        }
                    }
                }

                // Check if there are more items to process
                if (result.Items.Count < pageSize || skipCount + pageSize >= result.TotalCount)
                {
                    hasMore = false;
                }
                else
                {
                    skipCount += pageSize;
                }
            }

            // Generate next code: P + (maxNumber + 1) with 7 digits padding
            return $"P{(maxNumber + 1):D7}";
        }
        catch
        {
            // Fallback to P0000001 if error occurs
            return "P0000001";
        }
    }

    // -------------------------------
    // Document PDF Viewer Methods
    // -------------------------------

    private async Task OpenDocumentPdfViewerModalAsync(DocumentAssignmentWithNavigationPropertiesDto docAssignment)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            if (docAssignment?.Document == null)
            {
                return;
            }

            // Get document files for this document
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = docAssignment.Document.Id,
                MaxResultCount = 1,
                SkipCount = 0
            });
            
            
            var documentFile = documentFilesResult.Items.First();
            string path = documentFile.DocumentFile?.Path ?? string.Empty;

            if (!HC.Blazor.Shared.FileHelper.IsPdfFileExtension(documentFile.DocumentFile?.Name ?? string.Empty) 
            || string.IsNullOrEmpty(path)
            || !HC.Blazor.Shared.FileHelper.IsPdfFileExtension(path))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"], 
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            var fileBytes = await BlobContainer.GetAllBytesAsync(path);
            var base64 = Convert.ToBase64String(fileBytes);
            DocumentPdfFileUrl = $"data:application/pdf;base64,{base64}";
            IsDocumentPdfFile = true;
            await DocumentPdfViewerModal.Show();
            await BlockUiService.UnBlock();
        }
        catch (Exception ex)
        {
            await UiMessageService.Warn(L["NoPdfAvailable"] + ": " + ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await BlockUiService.UnBlock();
            return;
        }
    }


    private async Task CloseDocumentPdfViewerModalAsync()
    {
        if (DocumentPdfViewerModal != null)
        {
            await DocumentPdfViewerModal.Hide();
        }
        DocumentPdfFileUrl = null;
        IsDocumentPdfFile = false;
    }

    // -------------------------------
    // Create Task Modal Methods
    // -------------------------------

    private async Task OpenCreateTaskModalAsync()
    {
        try
        {
            if (CreateTaskModal != null)
            {
                await CreateTaskModal.OpenCreateProjectTaskModalAsync();
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OnTaskCreatedAsync()
    {
        // Reload tasks data when a new task is created
        await LoadMyTasksAsync();
        await LoadTasksStatisticsAsync();
    }

    private async Task OnTaskUpdatedAsync()
    {
        // Reload tasks data when a task is updated
        await LoadMyTasksAsync();
        await LoadTasksStatisticsAsync();
    }

    private async Task<bool> CheckIfDocumentHasPdfAsync(Guid documentId)
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
            return HC.Blazor.Shared.FileHelper.IsPdfFileExtension(documentFile.DocumentFile.Name) && !string.IsNullOrEmpty(documentFile.DocumentFile.Path);
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------
    // Notification Detail Modal Methods
    // -------------------------------

    private async Task ViewNotificationDetailAsync(NotificationReceiverWithNavigationPropertiesDto notification)
    {
        try
        {
            SelectedNotification = notification;
            // Mark as read if not already read
            if (!notification.NotificationReceiver.IsRead)
            {
                notification.NotificationReceiver.IsRead = true;
                await MarkNotificationAsReadAsync(notification);
            }
            
            if (NotificationDetailModal != null)
            {
                await NotificationDetailModal.Show();
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task MarkNotificationAsReadAsync(NotificationReceiverWithNavigationPropertiesDto item)
    {
        try
        {
            var updateDto = ObjectMapper.Map<NotificationReceiverDto, NotificationReceiverUpdateDto>(item.NotificationReceiver);
            updateDto.IsRead = true;
            updateDto.ReadAt = DateTime.UtcNow;
            await NotificationReceiversAppService.UpdateAsync(item.NotificationReceiver.Id, updateDto);
            
            // Reload notifications to update UI
            // await LoadRecentNotificationsAsync();
            
            // Send SignalR message to refresh unread count only on success
            if (CurrentUser.Id.HasValue)
            {
                await HubContext.Clients.User(CurrentUser.Id.Value.ToString())
                    .SendAsync("UnreadCountChanged");
            }
        }
        catch
        {
            await HandleErrorAsync(new Exception("Failed to mark notification as read"));
        }
    }

    private async Task CloseNotificationDetailModalAsync()
    {
        if (NotificationDetailModal != null)
        {
            await NotificationDetailModal.Hide();
        }
        SelectedNotification = null;
    }

    private string GetRelatedUrl(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.RelatedId) || string.IsNullOrEmpty(notification.RelatedType))
            return "#";

        var url = notification.RelatedType.ToUpper() switch
        {
            "TASK" => $"/project-task-detail/{notification.RelatedId}",
            "PROJECT" => $"/project-detail/{notification.RelatedId}",
            "DOCUMENT" => $"/document-detail/{notification.RelatedId}",
            "CALENDAR_EVENT" => $"/calendar-event-detail/{notification.RelatedId}",
            _ => "#"
        };
        return url ?? "#";
    }

    // -------------------------------
    // Date Range Filter Methods
    // -------------------------------

    private async Task OnDateRangeFilterChangedAsync()
    {
        try
        {
            // Update filter dates from selected range
            if (SelectedDateRange != null && SelectedDateRange.Count >= 2)
            {
                FilterStartDate = SelectedDateRange[0];
                FilterEndDate = SelectedDateRange[1];
            }
            else
            {
                // Reset to default if no valid range selected
                var today = DateTime.Now.Date;
                var sixtyDaysAgo = today.AddDays(-60);
                FilterStartDate = sixtyDaysAgo;
                FilterEndDate = today;
            }

            // Reload all data with new filter
            await LoadDashboardDataAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }



    private string DatePickerLocalizer(string name, params object[] arguments)
    {
        return name switch
        {
            "To" => L["DatePicker:To"],
            "From" => L["DatePicker:From"],
            "SelectDateRange" => L["DatePicker:SelectDateRange"],
            "FilterByDateRange" => L["DatePicker:FilterByDateRange"],
            "Search" => L["DatePicker:Search"],
            "Clear" => L["DatePicker:Clear"],
            "Cancel" => L["DatePicker:Cancel"],
            "Confirm" => L["DatePicker:Confirm"],
            _ => L[name] ?? name 
        };
    }
}