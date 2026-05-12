using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HC.CalendarEventParticipants;
using HC.CalendarEvents;
using HC.DocumentAssignments;
using HC.DocumentWorkflowInstanceLogss;
using HC.Documents;
using HC.NotificationReceivers;
using HC.ProjectTasks;
using HC.Projects;
using Microsoft.AspNetCore.Authorization;

namespace HC.Dashboard;

[Authorize]
public class HomeDashboardAppService : HCAppService, IHomeDashboardAppService
{
    private readonly IProjectsAppService _projectsAppService;
    private readonly IProjectTasksAppService _projectTasksAppService;
    private readonly ICalendarEventsAppService _calendarEventsAppService;
    private readonly ICalendarEventParticipantsAppService _calendarEventParticipantsAppService;
    private readonly IDocumentAssignmentsAppService _documentAssignmentsAppService;
    private readonly IDocumentsAppService _documentsAppService;
    private readonly INotificationReceiversAppService _notificationReceiversAppService;
    private readonly IDocumentWorkflowInstanceLogssAppService _documentWorkflowInstanceLogssAppService;

    public HomeDashboardAppService(
        IProjectsAppService projectsAppService,
        IProjectTasksAppService projectTasksAppService,
        ICalendarEventsAppService calendarEventsAppService,
        ICalendarEventParticipantsAppService calendarEventParticipantsAppService,
        IDocumentAssignmentsAppService documentAssignmentsAppService,
        IDocumentsAppService documentsAppService,
        INotificationReceiversAppService notificationReceiversAppService,
        IDocumentWorkflowInstanceLogssAppService documentWorkflowInstanceLogssAppService)
    {
        _projectsAppService = projectsAppService;
        _projectTasksAppService = projectTasksAppService;
        _calendarEventsAppService = calendarEventsAppService;
        _calendarEventParticipantsAppService = calendarEventParticipantsAppService;
        _documentAssignmentsAppService = documentAssignmentsAppService;
        _documentsAppService = documentsAppService;
        _notificationReceiversAppService = notificationReceiversAppService;
        _documentWorkflowInstanceLogssAppService = documentWorkflowInstanceLogssAppService;
    }

    public virtual async Task<HomeDashboardBundleDto> GetDashboardBundleAsync(GetHomeDashboardBundleInput input)
    {
        input ??= new GetHomeDashboardBundleInput();

        var today = Clock.Now.Date;
        var filterStart = input.StartDate?.Date ?? today.AddDays(-60);
        var filterEnd = input.EndDate?.Date ?? today;
        var filterEndExclusive = filterEnd.Date.AddDays(1).AddSeconds(-1);

        var culture = !string.IsNullOrWhiteSpace(input.Culture)
            ? input.Culture!
            : CultureInfo.CurrentUICulture.Name;

        var bundle = new HomeDashboardBundleDto();

        // Do not run multiple app-service / repository calls in parallel inside one UoW: ABP registers one
        // DbContext per unit of work; concurrent GetDbContextAsync hits "already contains a database API".
        var allProjects = await _projectsAppService.GetListAsync(new GetProjectsInput
        {
            MaxResultCount = 200,
            SkipCount = 0
        });

        var filteredProjects = await _projectsAppService.GetListAsync(new GetProjectsInput
        {
            MaxResultCount = 200,
            SkipCount = 0,
            StartDateMin = filterStart,
            StartDateMax = filterEndExclusive
        });

        var tasksInput = new GetProjectTasksInput
        {
            MaxResultCount = 200,
            SkipCount = 0,
            Sorting = "ProjectTask.StartDate DESC",
            StartDateMin = filterStart,
            StartDateMax = filterEndExclusive
        };

        var tasksPage = await _projectTasksAppService.GetListAsync(tasksInput);

        var calendarEvents = await LoadCalendarEventsForCurrentUserAsync(filterStart, filterEnd, filterEndExclusive);

        var assignmentResult = await _documentAssignmentsAppService.GetListAsync(new GetDocumentAssignmentsInput
        {
            ReceiverUserId = CurrentUser.Id,
            MaxResultCount = 10,
            SkipCount = 0,
            Sorting = "DocumentAssignment.CreationTime DESC",
            AssignedAtMin = filterStart,
            AssignedAtMax = filterEndExclusive
        });

        var personalResult = await _documentsAppService.GetListAsync(new GetDocumentsInput
        {
            SourceType = DocumentSourceType.Personal,
            IncommingDateMin = filterStart,
            IncommingDateMax = filterEnd,
            MaxResultCount = 10,
            SkipCount = 0,
            Sorting = "Document.CreationTime DESC"
        });

        var notifications = await _notificationReceiversAppService.GetMyListWithLocalizedMessagesAsync(new GetMyNotificationsInput
        {
            Culture = culture,
            MaxResultCount = 10,
            SkipCount = 0,
            Sorting = "NotificationReceiver.CreationTime DESC",
            CreationTimeMin = filterStart,
            CreationTimeMax = filterEndExclusive
        });

        var workflowChartStatistics =
            await _documentWorkflowInstanceLogssAppService.GetWorkflowChartStatisticsAsync(filterStart, filterEnd);

        bundle.TotalProjectsCount = (int)allProjects.TotalCount;
        bundle.ActiveProjectsCount = (int)filteredProjects.TotalCount;
        bundle.ActiveProjects = filteredProjects.Items.ToList();

        bundle.TotalTasksCount = (int)tasksPage.TotalCount;
        bundle.TasksByStatus = tasksPage.Items
            .GroupBy(t => t.ProjectTask.Status)
            .ToDictionary(g => g.Key, g => g.Count());
        bundle.MyTasks = tasksPage.Items.ToList();

        bundle.CalendarEvents = calendarEvents;

        var assignedItems = assignmentResult.Items
            .Where(x => x.Document.SourceType == DocumentSourceType.Archive)
            .Select(x => new HomeRecentDocumentItemDto
            {
                Document = x.Document,
                Time = x.DocumentAssignment.CreationTime
            });

        var personalItems = personalResult.Items.Select(x => new HomeRecentDocumentItemDto
        {
            Document = x.Document,
            Time = x.Document.CreationTime
        });

        bundle.RecentDocuments = assignedItems
            .Concat(personalItems)
            .OrderByDescending(x => x.Time)
            .Take(10)
            .ToList();

        bundle.RecentNotifications = notifications.Items.ToList();

        bundle.WorkflowChartStatistics = workflowChartStatistics;

        return bundle;
    }

    private async Task<List<CalendarEventDto>> LoadCalendarEventsForCurrentUserAsync(
        DateTime filterStart,
        DateTime filterEnd,
        DateTime filterEndExclusive)
    {
        if (!CurrentUser.Id.HasValue)
        {
            return new List<CalendarEventDto>();
        }

        var input = new GetCalendarEventsInput
        {
            MaxResultCount = 200,
            SkipCount = 0,
            Sorting = "StartTime"
        };

        input.StartTimeMax = filterEndExclusive;
        input.EndTimeMin = filterStart.Date;
        

        var result = await _calendarEventsAppService.GetListAsync(input);

        var participantsResult = await _calendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
        {
            IdentityUserId = CurrentUser.Id,
            MaxResultCount = 200,
            SkipCount = 0,
            Sorting = "CalendarEventParticipant.CreationTime DESC"
        });

        var participantEventIds = participantsResult.Items
            .Where(x => x.CalendarEvent != null)
            .Select(x => x.CalendarEvent!.Id)
            .ToHashSet();

        return result.Items
            .Where(x => participantEventIds.Contains(x.Id))
            .ToList();
    }
}
