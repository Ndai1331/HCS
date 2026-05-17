using Microsoft.AspNetCore.Components;
using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.CalendarEvents;
using HC.CalendarEventParticipants;
using HC.ProjectMembers;
using HC.Dashboard;
using HC.Documents;
using HC.NotificationReceivers;
using HC.Notifications;
using HC.DocumentFiles;
using Humanizer;
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
using HC.Chat.Conversations;
using Microsoft.Extensions.Logging;

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
    [Inject] private INotificationReceiversAppService NotificationReceiversAppService { get; set; } = default!;
    [Inject] private IHomeDashboardAppService HomeDashboardAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private HC.DocumentPdfViewer.IDocumentPdfViewerAppService DocumentPdfViewerAppService { get; set; } = default!;
    [Inject] private IBlockUiService BlockUiService { get; set; } = default!;
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;
    [Inject] private IHubContext<NotificationHub> HubContext { get; set; } = null!;
    [Inject] private ICalendarEventParticipantsAppService CalendarEventParticipantsAppService { get; set; } = default!;
    [Inject] private IProjectMembersAppService ProjectMembersAppService { get; set; } = default!;
    [Inject] private IConversationAppService ConversationAppService { get; set; } = default!;
    [Inject] private ILogger<Index> Logger { get; set; } = default!;

    // Active Projects data
    private List<ProjectWithNavigationPropertiesDto> ActiveProjectsList { get; set; } = new();
    private int TotalProjectsCount { get; set; }
    private int ActiveProjectsCount { get; set; }

    // Tasks data
    private Dictionary<string, int> TasksByStatus { get; set; } = new();
    private int TotalTasksCount { get; set; }

    // Calendar events data
    private List<CalendarEventDto> CalendarEventsList { get; set; } = new();

    private sealed class RecentDocumentItem
    {
        public DocumentDto Document { get; init; } = null!;
        public DateTime Time { get; init; }
    }

    // Documents data - combined personal docs + assigned docs for current user
    private List<RecentDocumentItem> RecentDocumentsList { get; set; } = new();
    private int WorkflowSignedCount { get; set; }
    private int WorkflowUnsignedCount { get; set; }
    private int WorkflowReturnedOrRejectedCount { get; set; }
    private int WorkflowTotalCount { get; set; }

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
    private Guid? CurrentDocumentPdfDocumentId { get; set; }

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
    private Guid? CurrentTaskPdfDocumentId { get; set; }
    private Modal PdfViewerModal { get; set; } = default!;
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    private bool IsLoading { get; set; } = true;
    private string LastNotificationTimeAgo { get; set; } = string.Empty;
    private string LastDocumentTimeAgo { get; set; } = string.Empty;
    private int TotalEvents { get; set; } = 0;

    // Calendar Event Detail Modals
    private Modal CalendarEventViewModal { get; set; } = new();
    private CalendarEventDto? ViewingCalendarEvent { get; set; }
    private IReadOnlyList<CalendarEventParticipantWithNavigationPropertiesDto> ViewingEventParticipants { get; set; } = new List<CalendarEventParticipantWithNavigationPropertiesDto>();

    // Project Detail Modal
    private Modal ProjectDetailModal { get; set; } = new();
    private ProjectDto? ViewingProject { get; set; }
    private string? ViewingProjectDepartmentName { get; set; }
    private IReadOnlyList<ProjectMemberWithNavigationPropertiesDto> ProjectMembersList { get; set; } = new List<ProjectMemberWithNavigationPropertiesDto>();
    private IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> ProjectTasksList { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();
    private string SelectedProjectDetailTab = "general";

    protected override async Task OnInitializedAsync()
    {
        var today = DateTime.Now.Date;
        var nextNinetyDays = today.AddDays(90);
        SelectedDateRange = new List<DateTime?> { today, nextNinetyDays };
        FilterStartDate = today;
        FilterEndDate = nextNinetyDays;

        await LoadDashboardDataAsync();
    }

    private async Task LoadDashboardDataAsync()
    {
        IsLoading = true;
        StateHasChanged();

        try
        {
            var bundle = await HomeDashboardAppService.GetDashboardBundleAsync(new GetHomeDashboardBundleInput
            {
                StartDate = FilterStartDate,
                EndDate = FilterEndDate,
                Culture = CultureInfo.CurrentUICulture.Name
            });

            TotalProjectsCount = bundle.TotalProjectsCount;
            ActiveProjectsCount = bundle.ActiveProjectsCount;
            ActiveProjectsList = bundle.ActiveProjects;

            TotalTasksCount = bundle.TotalTasksCount;
            TasksByStatus = bundle.TasksByStatus;
            MyTasksList = bundle.MyTasks;

            CalendarEventsList = bundle.CalendarEvents;
            TotalEvents = CalendarEventsList.Count;

            RecentDocumentsList = bundle.RecentDocuments
                .Select(x => new RecentDocumentItem { Document = x.Document, Time = x.Time })
                .ToList();
            LastDocumentTimeAgo = RecentDocumentsList.Any()
                ? RecentDocumentsList.First().Time.Humanize()
                : string.Empty;

            WorkflowSignedCount = bundle.WorkflowChartStatistics.SignedCount;
            WorkflowUnsignedCount = bundle.WorkflowChartStatistics.UnsignedCount;
            WorkflowReturnedOrRejectedCount = bundle.WorkflowChartStatistics.ReturnedOrRejectedCount;
            WorkflowTotalCount = bundle.WorkflowChartStatistics.TotalCount;

            RecentNotificationsList = bundle.RecentNotifications
                .Select(x => (NotificationReceiverWithNavigationPropertiesDto)x)
                .ToList();
            LastNotificationTimeAgo = bundle.RecentNotifications.Any()
                ? bundle.RecentNotifications.Last().NotificationReceiver.CreationTime.Humanize()
                : string.Empty;

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

    private double GetWorkflowPercentage(int count)
    {
        if (WorkflowTotalCount <= 0)
        {
            return 0;
        }

        return Math.Round((double)count / WorkflowTotalCount * 100, 1);
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

            CurrentTaskPdfDocumentId = projectTaskDocument.Document.Id;

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
                    // Get watermarked PDF from API (user + timestamp stamped)
                    var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
                    {
                        BlobPath = documentFile.DocumentFile.Path,
                        WatermarkAction = "view"
                    });

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
        CurrentTaskPdfDocumentId = null;
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

            await LoadDashboardDataAsync();

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
            const int pageSize = 500;
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

    private async Task OpenDocumentPdfViewerModalAsync(RecentDocumentItem docItem)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            if (docItem?.Document == null)
            {
                return;
            }

            CurrentDocumentPdfDocumentId = docItem.Document.Id;

            // Get document files for this document
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = docItem.Document.Id,
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
                await BlockUiService.UnBlock();
                return;
            }
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = path,
                WatermarkAction = "view"
            });
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
        CurrentDocumentPdfDocumentId = null;
    }

    private async Task AssignTaskFromTaskPdfViewerAsync()
    {
        if (!CurrentTaskPdfDocumentId.HasValue)
        {
            return;
        }

        var documentId = CurrentTaskPdfDocumentId.Value;
        await ClosePdfViewerModalAsync();
        await CreateTaskModal.OpenCreateProjectTaskModalAsync(documentId);
    }

    private async Task AssignTaskFromDocumentPdfViewerAsync()
    {
        if (!CurrentDocumentPdfDocumentId.HasValue)
        {
            return;
        }

        var documentId = CurrentDocumentPdfDocumentId.Value;
        await CloseDocumentPdfViewerModalAsync();
        await CreateTaskModal.OpenCreateProjectTaskModalAsync(documentId);
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
        await LoadDashboardDataAsync();
    }

    private async Task OnTaskUpdatedAsync()
    {
        await LoadDashboardDataAsync();
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
        if (string.IsNullOrEmpty(notification.RelatedId))
            return "#";

        var related = notification.RelatedType?.ToUpperInvariant() ?? string.Empty;
        if (related == "APPROVAL_DOCUMENT")
        {
            return $"/manage-documents?sourceType={(int)DocumentSourceType.SentToMe}&relatedId={Uri.EscapeDataString(notification.RelatedId)}";
        }

        if (string.IsNullOrEmpty(notification.RelatedType))
            return "#";

        var url = related switch
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

    // -------------------------------
    // Calendar Event Detail Modal Methods
    // -------------------------------

    private async Task OpenCalendarEventDetailModalAsync(CalendarEventDto calendarEvent)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Check if event has RelatedType and navigate accordingly
            if (Enum.TryParse<HC.CalendarEvents.RelatedType>(calendarEvent.RelatedType, out var relatedType))
            {
                Logger.LogInformation($"OpenCalendarEventDetailModalAsync: relatedType: {relatedType}, relatedId: {calendarEvent.RelatedId}");
                if (relatedType == HC.CalendarEvents.RelatedType.PROJECT && !string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
                {
                    // Try to find project by Code
                    var input = new GetProjectsInput
                    {
                        FilterText = calendarEvent.RelatedId,
                        MaxResultCount = 1,
                        SkipCount = 0
                    };
                    var result = await ProjectsAppService.GetListAsync(input);
                    if (result.Items.Any())
                    {
                        var project = result.Items.First().Project;
                        await OpenProjectDetailModalAsync(project.Id);
                        return;
                    }
                }
                else if (relatedType == HC.CalendarEvents.RelatedType.TASK && !string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
                {
                    // Try to find task by Code
                    var result = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(Guid.Parse(calendarEvent.RelatedId));
                    if (result != null)
                    {
                        await OpenTaskDetailModalAsync(result);
                        return;
                    }
                }
            }

            // Default: open event view modal
            await OpenEventViewModalAsync(calendarEvent);
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

    private async Task OpenEventViewModalAsync(CalendarEventDto calendarEvent)
    {
        try
        {
            ViewingCalendarEvent = await CalendarEventsAppService.GetAsync(calendarEvent.Id);

            // Load participants
            var participantsResult = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
            {
                CalendarEventId = calendarEvent.Id,
                MaxResultCount = 200,
                SkipCount = 0
            });
            ViewingEventParticipants = participantsResult.Items;

            await CalendarEventViewModal.Show();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEventViewModalAsync()
    {
        await CalendarEventViewModal.Hide();
        ViewingCalendarEvent = null;
        ViewingEventParticipants = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    }

    // -------------------------------
    // Project Detail Modal Methods
    // -------------------------------

    private async Task OpenProjectDetailModalAsync(Guid projectId)
    {
        try
        {
            ViewingProject = await ProjectsAppService.GetAsync(projectId);

            // Try to get department name from ActiveProjectsList
            var projectNav = ActiveProjectsList.FirstOrDefault(p => p.Project.Id == projectId);
            ViewingProjectDepartmentName = projectNav?.OwnerDepartment?.Name;

            // Load project members and tasks in parallel
            var membersTask = ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
            {
                ProjectId = projectId,
                MaxResultCount = 200,
                SkipCount = 0
            });
            var tasksTask = ProjectTasksAppService.GetListAsync(new GetProjectTasksInput
            {
                ProjectId = projectId,
                MaxResultCount = 200,
                SkipCount = 0
            });

            await Task.WhenAll(membersTask, tasksTask);
            var members = await membersTask;
            var tasks = await tasksTask;
            ProjectMembersList = members.Items;
            ProjectTasksList = tasks.Items;

            await ProjectDetailModal.Show();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseProjectDetailModalAsync()
    {
        await ProjectDetailModal.Hide();
        ViewingProject = null;
        ProjectMembersList = new List<ProjectMemberWithNavigationPropertiesDto>();
        ProjectTasksList = new List<ProjectTaskWithNavigationPropertiesDto>();
    }

    private void OnSelectedProjectDetailTabChanged(string name)
    {
        SelectedProjectDetailTab = name;
    }

    /// <summary>
    /// Get badge Color enum for task status string (used in project detail modal)
    /// </summary>
    private Color GetStatusBadgeColorEnum(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Color.Secondary;
        if (Enum.TryParse<ProjectTaskStatus>(status, out var parsed))
        {
            return EnumStatusColorHelper.GetProjectTaskStatusBadgeColor(parsed);
        }
        return Color.Secondary;
    }

    /// <summary>
    /// Get member initial letter for avatar circle
    /// </summary>
    private string GetMemberInitial(ProjectMemberWithNavigationPropertiesDto member)
    {
        var name = (member.User?.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Substring(0, 1).ToUpperInvariant();
        }

        var userName = (member.User?.UserName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Substring(0, 1).ToUpperInvariant();
        }

        return "?";
    }

    /// <summary>
    /// Open task detail modal from within the project detail modal
    /// </summary>
    private async Task OpenTaskDetailFromProjectModalAsync(ProjectTaskWithNavigationPropertiesDto task)
    {
        // Close the project detail modal first
        await ProjectDetailModal.Hide();
        // Open task detail modal
        await OpenTaskDetailModalAsync(task);
    }

    /// <summary>
    /// Get participant initial letter for avatar circle
    /// </summary>
    private string GetParticipantInitial(CalendarEventParticipantWithNavigationPropertiesDto participant)
    {
        var name = (participant.IdentityUser?.Name ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name.Substring(0, 1).ToUpperInvariant();
        }

        var userName = (participant.IdentityUser?.UserName ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(userName))
        {
            return userName.Substring(0, 1).ToUpperInvariant();
        }

        return "?";
    }

    /// <summary>
    /// Get badge color for participant response status
    /// </summary>
    private Color GetParticipantResponseColor(string? responseStatus)
    {
        return responseStatus switch
        {
            "ACCEPTED" => Color.Success,
            "DECLINED" => Color.Danger,
            "TENTATIVE" => Color.Warning,
            "INVITED" => Color.Primary,
            _ => Color.Secondary,
        };
    }

    // -------------------------------
    // Project Chat Navigation
    // -------------------------------

    private async Task NavigateToProjectChatAsync(ProjectWithNavigationPropertiesDto project)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            var conversation = await ConversationAppService.FindConversationByProjectIdAsync(project.Project.Id);
            if (conversation == null)
            {
                // Create conversation if it doesn't exist
                var members = await ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
                {
                    ProjectId = project.Project.Id,
                    MaxResultCount = 100,
                    SkipCount = 0
                });

                var createInput = new CreateProjectConversationInput
                {
                    ProjectId = project.Project.Id,
                    Name = project.Project.Name,
                    MemberUserIds = members.Items.Select(m => m.User.Id).ToList()
                };
                conversation = await ConversationAppService.CreateProjectConversationAsync(createInput);

                if (conversation != null)
                {
                    await UiMessageService.Success(L["ProjectChatCreatedSuccessfully"],
                        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                }
                else
                {
                    await UiMessageService.Error(L["ProjectChatCreationFailed"],
                        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                }
            }
            else
            {
                if (conversation.Members.Any(m => m.UserId == CurrentUser.Id))
                {
                    Navigation.NavigateTo($"/chat/{conversation.Id}");
                }
                else
                {
                    await UiMessageService.Error(L["YouAreNotAMemberOfThisProjectChat"],
                        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                }
            }
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
}
