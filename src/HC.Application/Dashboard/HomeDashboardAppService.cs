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
using HC.Permissions;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;

namespace HC.Dashboard;

[Authorize]
public class HomeDashboardAppService : HCAppService, IHomeDashboardAppService
{
    private readonly IHomeDashboardQueryRepository _homeDashboardQueryRepository;
    private readonly ICalendarEventsAppService _calendarEventsAppService;
    private readonly ICalendarEventParticipantsAppService _calendarEventParticipantsAppService;
    private readonly IDocumentAssignmentsAppService _documentAssignmentsAppService;
    private readonly IDocumentsAppService _documentsAppService;
    private readonly INotificationReceiversAppService _notificationReceiversAppService;
    private readonly IDocumentWorkflowInstanceLogssAppService _documentWorkflowInstanceLogssAppService;

    public HomeDashboardAppService(
        IHomeDashboardQueryRepository homeDashboardQueryRepository,
        ICalendarEventsAppService calendarEventsAppService,
        ICalendarEventParticipantsAppService calendarEventParticipantsAppService,
        IDocumentAssignmentsAppService documentAssignmentsAppService,
        IDocumentsAppService documentsAppService,
        INotificationReceiversAppService notificationReceiversAppService,
        IDocumentWorkflowInstanceLogssAppService documentWorkflowInstanceLogssAppService)
    {
        _homeDashboardQueryRepository = homeDashboardQueryRepository;
        _calendarEventsAppService = calendarEventsAppService;
        _calendarEventParticipantsAppService = calendarEventParticipantsAppService;
        _documentAssignmentsAppService = documentAssignmentsAppService;
        _documentsAppService = documentsAppService;
        _notificationReceiversAppService = notificationReceiversAppService;
        _documentWorkflowInstanceLogssAppService = documentWorkflowInstanceLogssAppService;
    }

    [Authorize(HCPermissions.Workspace.Default)]
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

        var summary = await _homeDashboardQueryRepository.GetProjectAndTaskSummaryAsync(
            filterStart, filterEndExclusive, maxListItems: 200);

        bundle.TotalProjectsCount = (int)summary.TotalProjectsCount;
        bundle.ActiveProjectsCount = (int)summary.FilteredProjectsCount;
        bundle.ActiveProjects = ObjectMapper.Map<List<ProjectWithNavigationProperties>, List<ProjectWithNavigationPropertiesDto>>(summary.FilteredProjects);
        bundle.TotalTasksCount = (int)summary.TotalTasksCount;
        bundle.TasksByStatus = summary.TasksByStatus;
        bundle.MyTasks = ObjectMapper.Map<List<ProjectTaskWithNavigationProperties>, List<ProjectTaskWithNavigationPropertiesDto>>(summary.FilteredTasks);

        bundle.CalendarEvents = await LoadCalendarEventsForCurrentUserAsync(filterStart, filterEnd, filterEndExclusive);

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

        bundle.WorkflowChartStatistics =
            await _documentWorkflowInstanceLogssAppService.GetWorkflowChartStatisticsAsync(filterStart, filterEnd);

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
