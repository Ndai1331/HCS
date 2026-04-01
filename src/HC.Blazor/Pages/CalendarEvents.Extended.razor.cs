using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Blazorise;
using HC.CalendarEvents;
using HC.CalendarEventParticipants;
using HC.Projects;
using HC.ProjectTasks;
using HC.ProjectMembers;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.DocumentFiles;
using Microsoft.AspNetCore.Components;
using Volo.Abp.BlobStoring;
using HC.Blazor.Components.ProjectTaskViewModal;

namespace HC.Blazor.Pages;

public partial class CalendarEvents
{
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private HC.DocumentPdfViewer.IDocumentPdfViewerAppService DocumentPdfViewerAppService { get; set; } = default!;

    // PDF viewer for task documents
    private Modal PdfViewerModal { get; set; } = new();
    private string? PdfFileUrl { get; set; }
    private bool IsPdfFile { get; set; }
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    // Modal for displaying day events with tabs
    protected Modal DayEventsModal { get; set; } = new();
    protected DateOnly SelectedDayDate { get; set; }
    protected string SelectedDayTab = "events"; // events, projects, tasks
    protected List<CalendarEventDto> SelectedDayEvents { get; set; } = new();
    protected List<CalendarEventDto> SelectedDayProjectEvents { get; set; } = new();
    protected List<CalendarEventDto> SelectedDayTaskEvents { get; set; } = new();

    // Event View Modal (Read-only)
    protected Modal EventViewModal { get; set; } = new();
    protected CalendarEventDto? ViewingCalendarEvent { get; set; }
    protected IReadOnlyList<CalendarEventParticipantWithNavigationPropertiesDto> ViewingEventParticipants { get; set; } = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    protected string SelectedEventViewTab = "general";

    // Project Detail Modal
    protected Modal ProjectDetailModal { get; set; } = new();
    protected ProjectDto? ViewingProject { get; set; }
    protected string? ViewingProjectDepartmentName { get; set; }
    protected IReadOnlyList<ProjectMemberWithNavigationPropertiesDto> ProjectMembersList { get; set; } = new List<ProjectMemberWithNavigationPropertiesDto>();
    protected IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> ProjectTasksList { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();
    protected string SelectedProjectDetailTab = "general";

    // Task Detail Modal
    protected ProjectTaskViewModal TaskDetailModal { get; set; } = new();

    // Open day events modal when clicking on a day in scheduler
    protected async Task OnSchedulerDayClicked(DateTime clickedDate)
    {
        try
        {
            var dateOnly = DateOnly.FromDateTime(clickedDate);
            SelectedDayDate = dateOnly;

            // Filter events for the selected day
            SelectedDayEvents = CalendarEventList
                .Where(e => e.StartTime.Date <= clickedDate.Date && e.EndTime.Date >= clickedDate.Date)
                .ToList();

            // Group events by RelatedType
            SelectedDayProjectEvents = SelectedDayEvents
                .Where(e => e.RelatedType == RelatedType.PROJECT.ToString())
                .ToList();

            SelectedDayTaskEvents = SelectedDayEvents
                .Where(e => e.RelatedType == RelatedType.TASK.ToString())
                .ToList();

            // Default to events tab if there are any events that are not projects or tasks
            var hasOtherEvents = SelectedDayEvents.Any(e =>
                e.RelatedType == RelatedType.NONE.ToString() ||
                (!Enum.TryParse<RelatedType>(e.RelatedType, out var relatedType) || relatedType == RelatedType.NONE));

            SelectedDayTab = hasOtherEvents ? "events" :
                           SelectedDayProjectEvents.Any() ? "projects" :
                           "tasks";

            await DayEventsModal.Show();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected async Task CloseDayEventsModalAsync()
    {
        await DayEventsModal.Hide();
    }

    protected void OnDayEventsModalClosed()
    {
        SelectedDayEvents = new List<CalendarEventDto>();
        SelectedDayProjectEvents = new List<CalendarEventDto>();
        SelectedDayTaskEvents = new List<CalendarEventDto>();
        SelectedDayTab = "events";
    }

    protected void OnSelectedDayTabChanged(string name)
    {
        SelectedDayTab = name;
    }

    protected IEnumerable<CalendarEventDto> GetSelectedDayStandaloneEvents()
    {
        return SelectedDayEvents.Where(e =>
            e.RelatedType == RelatedType.NONE.ToString() ||
            (!Enum.TryParse<RelatedType>(e.RelatedType, out var relatedType) || relatedType == RelatedType.NONE));
    }

    // Get display code for RelatedId (extract Code from RelatedId field)
    protected string GetRelatedCode(CalendarEventDto calendarEvent)
    {
        return calendarEvent.RelatedId ?? string.Empty;
    }

    // Get display title for event card
    protected string GetEventCardTitle(CalendarEventDto calendarEvent)
    {
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            if (relatedType == RelatedType.PROJECT)
            {
                return $"📁 {calendarEvent.Title} ({GetRelatedCode(calendarEvent)})";
            }
            else if (relatedType == RelatedType.TASK)
            {
                return $"✅ {calendarEvent.Title} ({GetRelatedCode(calendarEvent)})";
            }
        }
        return $"📅 {calendarEvent.Title}";
    }

    protected string GetDayEventTypeLabel(CalendarEventDto calendarEvent)
    {
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            return relatedType switch
            {
                RelatedType.PROJECT => L["Projects"],
                RelatedType.TASK => L["Tasks"],
                _ => L["Events"]
            };
        }

        return L["Events"];
    }

    protected string GetDayEventTypeClass(CalendarEventDto calendarEvent)
    {
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            return relatedType switch
            {
                RelatedType.PROJECT => "hc-day-item-tag hc-day-item-tag-project",
                RelatedType.TASK => "hc-day-item-tag hc-day-item-tag-task",
                _ => "hc-day-item-tag hc-day-item-tag-event"
            };
        }

        return "hc-day-item-tag hc-day-item-tag-event";
    }

    protected string GetDayEventTimeDisplay(CalendarEventDto calendarEvent)
    {
        return calendarEvent.AllDay
            ? L["AllDay"]
            : $"{calendarEvent.StartTime:HH:mm} - {calendarEvent.EndTime:HH:mm}";
    }

    protected string GetDayEventReferenceLabel(CalendarEventDto calendarEvent)
    {
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            return relatedType switch
            {
                RelatedType.PROJECT => $"{L["Projects"]}:",
                RelatedType.TASK => $"{L["Tasks"]}:",
                _ => string.Empty
            };
        }

        return string.Empty;
    }

    protected string GetDayEventReferenceValue(CalendarEventDto calendarEvent)
    {
        return string.IsNullOrWhiteSpace(calendarEvent.RelatedId)
            ? calendarEvent.Title ?? string.Empty
            : calendarEvent.RelatedId;
    }

    // Navigate to related entity or open detail modal
    protected async Task NavigateToRelatedEntity(CalendarEventDto calendarEvent)
    {
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            if (relatedType == RelatedType.PROJECT && !string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
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
            else if (relatedType == RelatedType.TASK && !string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
            {
                // Try to find task by Code
                var input = new GetProjectTasksInput
                {
                    FilterText = calendarEvent.RelatedId,
                    MaxResultCount = 1,
                    SkipCount = 0
                };
                var result = await ProjectTasksAppService.GetListAsync(input);
                if (result.Items.Any())
                {
                    var task = result.Items.First().ProjectTask;
                    await OpenTaskDetailModalAsync(task.Id);
                    return;
                }
            }
        }

        // Default: open event view modal
        await OpenEventViewModalAsync(calendarEvent);
    }

    // Open Event View Modal (Read-only)
    protected async Task OpenEventViewModalAsync(CalendarEventDto calendarEvent)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            ViewingCalendarEvent = await CalendarEventsAppService.GetAsync(calendarEvent.Id);
            SelectedEventViewTab = "general";

            // Load participants
            var participantsResult = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
            {
                CalendarEventId = calendarEvent.Id,
                MaxResultCount = 1000,
                SkipCount = 0
            });
            ViewingEventParticipants = participantsResult.Items;

            await EventViewModal.Show();
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

    protected void CloseEventViewModalAsync()
    {
        SelectedEventViewTab = "general";
        ViewingCalendarEvent = null;
        ViewingEventParticipants = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    }

    protected void OnSelectedEventViewTabChanged(string name)
    {
        SelectedEventViewTab = name;
    }

    // Event handler for when EventViewModal is closed
    protected void OnEventViewModalClosed()
    {
        CloseEventViewModalAsync();
    }

    // Open Project Detail Modal (same as workspace Index page)
    protected async Task OpenProjectDetailModalAsync(Guid projectId)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            var projectNav = await ProjectsAppService.GetWithNavigationPropertiesAsync(projectId);
            ViewingProject = projectNav.Project;
            ViewingProjectDepartmentName = projectNav.OwnerDepartment?.Name;

            // Load project members and tasks in parallel
            var membersTask = ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
            {
                ProjectId = projectId,
                MaxResultCount = 1000,
                SkipCount = 0
            });
            var tasksTask = ProjectTasksAppService.GetListAsync(new GetProjectTasksInput
            {
                ProjectId = projectId,
                MaxResultCount = 1000,
                SkipCount = 0
            });

            await Task.WhenAll(membersTask, tasksTask);
            ProjectMembersList = membersTask.Result.Items;
            ProjectTasksList = tasksTask.Result.Items;

            SelectedProjectDetailTab = "general";
            await ProjectDetailModal.Show();
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

    protected async Task CloseProjectDetailModalAsync()
    {
        await ProjectDetailModal.Hide();
        ViewingProject = null;
        ViewingProjectDepartmentName = null;
        ProjectMembersList = new List<ProjectMemberWithNavigationPropertiesDto>();
        ProjectTasksList = new List<ProjectTaskWithNavigationPropertiesDto>();
    }

    // Event handler for when ProjectDetailModal is closed
    protected async Task OnProjectDetailModalClosed()
    {
        await CloseProjectDetailModalAsync();
    }

    protected void OnSelectedProjectDetailTabChanged(string name)
    {
        SelectedProjectDetailTab = name;
    }

    // Open Task Detail Modal
    protected async Task OpenTaskDetailModalAsync(Guid taskId)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            var task = await ProjectTasksAppService.GetWithNavigationPropertiesAsync(taskId);
            await TaskDetailModal.ShowAsync(task);
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

    /// <summary>
    /// Open task detail modal from within the project detail modal (same as workspace)
    /// </summary>
    protected async Task OpenTaskDetailFromProjectModalAsync(ProjectTaskWithNavigationPropertiesDto task)
    {
        await ProjectDetailModal.Hide();
        await OpenTaskDetailModalAsync(task.ProjectTask.Id);
    }

    /// <summary>
    /// Get member initial letter for avatar circle (same as workspace)
    /// </summary>
    protected string GetMemberInitial(ProjectMemberWithNavigationPropertiesDto member)
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
    /// Get badge Color enum for task status (same as workspace)
    /// </summary>
    protected Color GetStatusBadgeColorEnum(string? status)
    {
        if (string.IsNullOrWhiteSpace(status)) return Color.Secondary;
        if (Enum.TryParse<ProjectTaskStatus>(status, out var parsed))
        {
            return HC.Blazor.Shared.EnumStatusColorHelper.GetProjectTaskStatusBadgeColor(parsed);
        }
        return Color.Secondary;
    }

    /// <summary>
    /// Get participant initial letter for avatar circle (same as workspace)
    /// </summary>
    protected string GetParticipantInitial(CalendarEventParticipantWithNavigationPropertiesDto participant)
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
    /// Get badge color for participant response status (same as workspace)
    /// </summary>
    protected Color GetParticipantResponseColor(string? responseStatus)
    {
        if (string.IsNullOrWhiteSpace(responseStatus)) return Color.Secondary;
        if (Enum.TryParse<ParticipantResponse>(responseStatus, out var parsed))
        {
            return HC.Blazor.Shared.EnumStatusColorHelper.GetCalendarEventParticipantResponseBadgeColor(parsed);
        }
        return Color.Secondary;
    }

    // Event handler for when TaskDetailModal is closed - removed as it's handled by component

    // Helper methods for task detail display are now in the component, but we keep some for other uses
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
                    await PdfViewerModal.Show();
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
        PdfFileUrl = null;
        IsPdfFile = false;
    }

    // Event handler for when PdfViewerModal is closed
    private async Task OnPdfViewerModalClosed()
    {
        await ClosePdfViewerModalAsync();
    }
}
