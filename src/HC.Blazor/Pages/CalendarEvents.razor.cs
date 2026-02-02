using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Web;
using System.Threading;
using Blazorise;
using Blazorise.DataGrid;
using Blazorise.Scheduler;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.CalendarEvents;
using HC.CalendarEventParticipants;
using HC.ProjectTasks;
using HC.Projects;
using HC.Permissions;
using HC.Shared;
using Volo.Abp.Identity;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages;


public partial class CalendarEvents : HCComponentBase
{
    [Inject] private IProjectTasksAppService ProjectTasksAppService { get; set; } = default!;
    [Inject] private ICalendarEventParticipantsAppService CalendarEventParticipantsAppService { get; set; } = default!;
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;
    [Inject] private ILogger<CalendarEvents> Logger { get; set; } = default!;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; set; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    // View toggle
    protected bool IsListView { get; set; } = true;

    public DataGrid<CalendarEventDto> DataGridRef { get; set; }

    private IReadOnlyList<CalendarEventDto> CalendarEventList { get; set; }
  
    private List<Appointment> SchedulerEventList { get; set; } = new List<Appointment>();
    
    // Track the latest request to avoid race conditions when switching months quickly
    private long _lastUpdateRequestId = 0;

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateCalendarEvent { get; set; }

    private bool CanEditCalendarEvent { get; set; }

    private bool CanDeleteCalendarEvent { get; set; }

    private CalendarEventCreateDto NewCalendarEvent { get; set; }

    private CalendarEventUpdateDto EditingCalendarEvent { get; set; }
    private Guid EditingCalendarEventId { get; set; }

    private Modal CreateCalendarEventModal { get; set; } = new();
    private Modal EditCalendarEventModal { get; set; } = new();
    private GetCalendarEventsInput Filter { get; set; }

    private DataGridEntityActionsColumn<CalendarEventDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedEditTab = "calendarEvent-edit-tab";
    private CalendarEventDto? SelectedCalendarEvent;

    private List<CalendarEventDto> SelectedCalendarEvents { get; set; } = new();
    private bool AllCalendarEventsSelected { get; set; }

    // Enum properties for Create
    private EventType NewCalendarEventEventType { get; set; } = EventType.MEETING;
    private RelatedType NewCalendarEventRelatedType { get; set; } = RelatedType.NONE;
    private EventVisibility NewCalendarEventVisibility { get; set; } = EventVisibility.PRIVATE;

    // Enum properties for Edit
    private EventType EditingCalendarEventEventType { get; set; } = EventType.MEETING;
    private RelatedType EditingCalendarEventRelatedType { get; set; } = RelatedType.NONE;
    private EventVisibility EditingCalendarEventVisibility { get; set; } = EventVisibility.PRIVATE;

    // Enum properties for Filter
    private EventType? FilterEventType { get; set; }
    private RelatedType? FilterRelatedType { get; set; }
    private EventVisibility? FilterVisibility { get; set; }

    // Select2 for Projects
    protected sealed class ProjectSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
    private IReadOnlyList<ProjectSelectItem> ProjectsCollection { get; set; } = new List<ProjectSelectItem>();
    private List<ProjectSelectItem> SelectedFilterProject { get; set; } = new();
    private List<ProjectSelectItem> SelectedNewProject { get; set; } = new();
    private List<ProjectSelectItem> SelectedEditProject { get; set; } = new();

    // Select2 for ProjectTasks
    protected sealed class ProjectTaskSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }
    private IReadOnlyList<ProjectTaskSelectItem> ProjectTasksCollection { get; set; } = new List<ProjectTaskSelectItem>();
    private List<ProjectTaskSelectItem> SelectedFilterProjectTask { get; set; } = new();
    private List<ProjectTaskSelectItem> SelectedNewProjectTask { get; set; } = new();
    private List<ProjectTaskSelectItem> SelectedEditProjectTask { get; set; } = new();

    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Validation error keys
    private string? CreateCalendarEventValidationErrorKey { get; set; }
    private string? EditCalendarEventValidationErrorKey { get; set; }
    private string? CreateGeneralValidationErrorKey { get; set; }

    // Create wizard state (General -> Participants)
    private Guid CreateWizardCalendarEventId { get; set; }
    protected bool IsCreateWizardGeneralSaved => CreateWizardCalendarEventId != Guid.Empty;
    protected string SelectedCreateTab = "general";

    // Participants (create wizard)
    private IReadOnlyList<CalendarEventParticipantWithNavigationPropertiesDto> CreateParticipantsList { get; set; } = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    private List<LookupDto<Guid>> CreateParticipantsUserToAdd { get; set; } = new();
    private ParticipantResponse CreateParticipantResponseStatus { get; set; } = ParticipantResponse.INVITED;
    private IReadOnlyList<LookupDto<Guid>> ParticipantIdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();

    // Participants (edit)
    private IReadOnlyList<CalendarEventParticipantWithNavigationPropertiesDto> EditParticipantsList { get; set; } = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    private List<LookupDto<Guid>> EditParticipantsUserToAdd { get; set; } = new();
    private ParticipantResponse EditParticipantResponseStatus { get; set; } = ParticipantResponse.INVITED;

    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);


    // Scheduler properties
    private DateOnly SelectedSchedulerDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    private SchedulerView SelectedSchedulerView { get; set; } = SchedulerView.Month;
    private TimeOnly SchedulerStartTime { get; set; } = new TimeOnly(00, 00);
    private TimeOnly SchedulerEndTime { get; set; } = new TimeOnly(23, 59, 59);
    private TimeOnly SchedulerWorkDayStart { get; set; } = new TimeOnly(00, 00);
    private TimeOnly SchedulerWorkDayEnd { get; set; } = new TimeOnly(23, 59, 59);

    // Scheduler Localizers
    private SchedulerLocalizers SchedulerLocalizers { get; set; } = new();

    // DatePicker refs for StartTime and EndTime
    private DatePicker<DateTime>? NewCalendarEventStartTimeDatePicker { get; set; }
    private DatePicker<DateTime>? NewCalendarEventEndTimeDatePicker { get; set; }
    private DatePicker<DateTime>? EditingCalendarEventStartTimeDatePicker { get; set; }
    private DatePicker<DateTime>? EditingCalendarEventEndTimeDatePicker { get; set; }

    public CalendarEvents()
    {
        NewCalendarEvent = new CalendarEventCreateDto();
        EditingCalendarEvent = new CalendarEventUpdateDto();
        Filter = new GetCalendarEventsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        CalendarEventList = new List<CalendarEventDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        // Ensure SelectedSchedulerDate is first day of month if in Month view
        if (SelectedSchedulerView == SchedulerView.Month)
        {
            var firstDayOfMonth = new DateOnly(SelectedSchedulerDate.Year, SelectedSchedulerDate.Month, 1);
            if (SelectedSchedulerDate != firstDayOfMonth)
            {
                SelectedSchedulerDate = firstDayOfMonth;
            }
        }
        
        // Initialize filter enum values from Filter strings
        if (!string.IsNullOrWhiteSpace(Filter.EventType) && Enum.TryParse<EventType>(Filter.EventType, out var eventType))
        {
            FilterEventType = eventType;
        }
        if (!string.IsNullOrWhiteSpace(Filter.RelatedType) && Enum.TryParse<RelatedType>(Filter.RelatedType, out var relatedType))
        {
            FilterRelatedType = relatedType;
        }
        if (!string.IsNullOrWhiteSpace(Filter.Visibility) && Enum.TryParse<EventVisibility>(Filter.Visibility, out var visibility))
        {
            FilterVisibility = visibility;
        }
        
        await SetPermissionsAsync();
        InitializeSchedulerLocalizers();
        try
        {
            await GetProjectCollectionLookupAsync();
            // Load calendar events on initialization
            await GetCalendarEventsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void InitializeSchedulerLocalizers()
    {
        SchedulerLocalizers = new SchedulerLocalizers
        {
            TodayLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Today"]),
            DayLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Day"]),
            WeekLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Week"]),
            WorkWeekLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["WorkWeek"]),
            MonthLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Month"]),
            YearLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Year"]),
            OnLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["On"]),
            WeekOfMonthLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["WeekOfMonth"]),
            DayOfWeekLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["DayOfWeek"]),
            MonthOfYearLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["MonthOfYear"]),
            CountLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Count"]),
            NeverLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["Never"]),
            RepeatEveryLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["RepeatEvery"]),
            WhatDoYouWantToDoLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["WhatDoYouWantToDo"]),
            RecurringSeriesWhatDoYouWantToDoLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["RecurringSeriesWhatDoYouWantToDo"]),
            StartDateHigherValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["StartDateHigherValidation"]),
            StartDateHigherOrEqualValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["StartDateHigherOrEqualValidation"]),
            EndDateLowerValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["EndDateLowerValidation"]),
            EndDateLowerOrEqualValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["EndDateLowerOrEqualValidation"]),
            EndTimeLowerValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["EndTimeLowerValidation"]),
            TitleRequiredValidationLocalizer = new Blazorise.Localization.TextLocalizerHandler((name, arguments) => L["TitleRequiredValidation"]),
        };
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            // Ensure data is loaded for both views
            if (!CalendarEventList.Any())
            {
                await GetCalendarEventsAsync();
            }
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["CalendarEvents"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        RebuildToolbar();
        return ValueTask.CompletedTask;
    }

    private void RebuildToolbar()
    {
        Toolbar = new PageToolbar();
        // Toggle button: show "Calendar" icon and text when in List view, "List" icon and text when in Calendar view
        Toolbar.AddButton(
            IsListView ? L["Calendar"] : L["List"],
            async () => { await ToggleViewAsync(); },
            IsListView ? IconName.Calendar : IconName.List);
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewCalendarEvent"], async () => {
            NavigationManager.NavigateTo("/calendar-event-detail");
        }, IconName.Add, requiredPolicyName: HCPermissions.CalendarEvents.Create);
    }

    private async Task ToggleViewAsync()
    {
        IsListView = !IsListView;
        RebuildToolbar();
        
        await GetCalendarEventsAsync();
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<CalendarEventDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteCalendarEvent;


    private async Task SetPermissionsAsync()
    {
        CanCreateCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Create);
        CanEditCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Edit);
        CanDeleteCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Delete);
    }

    private async Task GetCalendarEventsAsync()
    {
        try
        {
            GetCalendarEventsInput calendarFilter;

            if (IsListView)
            {
                // List view: Use pagination with filter
                if (Filter == null)
                {
                    calendarFilter = new GetCalendarEventsInput
                    {
                        MaxResultCount = PageSize,
                        SkipCount = (CurrentPage - 1) * PageSize,
                        Sorting = CurrentSorting
                    };
                }
                else
                {
                    calendarFilter = Filter;
                    calendarFilter.MaxResultCount = PageSize;
                    calendarFilter.SkipCount = (CurrentPage - 1) * PageSize;
                    calendarFilter.Sorting = CurrentSorting;
                }
            }
            else
            {
                // Calendar view: Filter by SelectedSchedulerDate based on view type
                var startDate = DateTime.MinValue;
                var endDate = DateTime.MaxValue;

                if (SelectedSchedulerView == SchedulerView.Month)
                {
                    // For Month view, filter by the entire month
                    var firstDayOfMonth = new DateOnly(SelectedSchedulerDate.Year, SelectedSchedulerDate.Month, 1);
                    var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                    startDate = firstDayOfMonth.ToDateTime(TimeOnly.MinValue);
                    endDate = lastDayOfMonth.ToDateTime(TimeOnly.MaxValue);
                }
               
                calendarFilter = new GetCalendarEventsInput
                {
                    MaxResultCount = 1000, // Load all events for calendar (max limit)
                    SkipCount = 0,
                    Sorting = CurrentSorting,
                    StartTimeMin = startDate, 
                    EndTimeMax = startDate 
                };

                if (Filter != null)
                {
                    calendarFilter.FilterText = Filter.FilterText;
                    calendarFilter.Title = Filter.Title;
                    calendarFilter.Description = Filter.Description;
                    calendarFilter.AllDay = Filter.AllDay;
                    calendarFilter.EventType = Filter.EventType;
                    calendarFilter.Location = Filter.Location;
                    calendarFilter.RelatedType = Filter.RelatedType;
                    calendarFilter.RelatedId = Filter.RelatedId;
                    calendarFilter.Visibility = Filter.Visibility;
                    calendarFilter.StartTimeMin = startDate;
                    calendarFilter.EndTimeMax = endDate;
                }

            Logger.LogInformation("GetCalendarEventsAsync - IsListView: {IsList}, SelectedSchedulerView: {View}, SelectedSchedulerDate: {Date}, StartDate: {StartDate}, EndDate: {EndDate}",
                IsListView, SelectedSchedulerView, SelectedSchedulerDate, startDate, endDate);
            }
            

            var result = await CalendarEventsAppService.GetListAsync(calendarFilter);
            CalendarEventList = result.Items ?? new List<CalendarEventDto>();
            TotalCount = (int)(result?.TotalCount ?? 0);
            
            Logger.LogInformation("GetCalendarEventsAsync - API Result - TotalCount: {Total}, Items Count: {Items}, CalendarEventList Count: {ListCount}",
                TotalCount, result?.Items?.Count ?? 0, CalendarEventList.Count);
            
            // Pre-load participant counts for all events
            if (CalendarEventList.Any())
            {
                var eventIds = CalendarEventList.Select(e => e.Id).ToList();
                var tasks = eventIds.Select(async id =>
                {
                    try
                    {
                        var countResult = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
                        {
                            CalendarEventId = id,
                            MaxResultCount = 1,
                            SkipCount = 0
                        });
                        _participantCountCache[id] = (int)countResult.TotalCount;
                    }
                    catch
                    {
                        _participantCountCache[id] = 0;
                    }
                });
                await Task.WhenAll(tasks);
            }
            
            if (!IsListView && CalendarEventList.Any())
            {
                var firstDayOfMonth = new DateOnly(SelectedSchedulerDate.Year, SelectedSchedulerDate.Month, 1);
                if (SelectedSchedulerDate != firstDayOfMonth)
                {
                    SelectedSchedulerDate = firstDayOfMonth;
                }
            }
            
            await ClearSelection();
            
            await UpdateTestAppointmentsFromCalendarEvents();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            CalendarEventList = new List<CalendarEventDto>();
            SchedulerEventList.Clear();
            TotalCount = 0;
        }
    }

    // Method to convert CalendarEventDto to Appointment and update Appointments list
    private async Task UpdateTestAppointmentsFromCalendarEvents()
    {
        // Generate unique request ID for this update
        var requestId = Interlocked.Increment(ref _lastUpdateRequestId);
        var currentSelectedDate = SelectedSchedulerDate;
        var currentView = SelectedSchedulerView;
        
        try
        {
            Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Start [RequestId: {RequestId}] - CalendarEventList Count: {Count}, SelectedSchedulerView: {View}, SelectedSchedulerDate: {Date}",
                requestId, CalendarEventList?.Count ?? 0, currentView, currentSelectedDate);

            if (CalendarEventList == null || !CalendarEventList.Any())
            {
                Logger.LogWarning("UpdateTestAppointmentsFromCalendarEvents - CalendarEventList is null or empty");
                return;
            }

            var testAppointments = new List<Appointment>();
            var isMonthView = SelectedSchedulerView == SchedulerView.Month;

            if (isMonthView)
            {
                // For Month view, split multi-day events into single-day appointments
                var firstDayOfMonth = new DateOnly(SelectedSchedulerDate.Year, SelectedSchedulerDate.Month, 1);
                var lastDayOfMonth = firstDayOfMonth.AddMonths(1).AddDays(-1);
                
                Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Month View - FirstDay: {FirstDay}, LastDay: {LastDay}, Total Events: {Count}",
                    firstDayOfMonth, lastDayOfMonth, CalendarEventList.Count);

                foreach (var evt in CalendarEventList)
                {
                    var startDate = evt.StartTime.Date;
                    var endDate = evt.EndTime.Date;
                    
                    var firstDayDateTime = firstDayOfMonth.ToDateTime(TimeOnly.MinValue);
                    var lastDayDateTime = lastDayOfMonth.ToDateTime(TimeOnly.MaxValue);

                    Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Processing event - Id: {Id}, Title: {Title}, StartDate: {StartDate}, EndDate: {EndDate}, FirstDayOfMonth: {FirstDay}, LastDayOfMonth: {LastDay}",
                        evt.Id, evt.Title, startDate, endDate, firstDayDateTime, lastDayDateTime);

                    // Only process events that overlap with the current month
                    if (endDate < firstDayDateTime || startDate > lastDayDateTime)
                    {
                        Logger.LogWarning("UpdateTestAppointmentsFromCalendarEvents - Event skipped (outside month range) - Id: {Id}, Title: {Title}, StartDate: {StartDate}, EndDate: {EndDate}, Condition: endDate < firstDay ({EndBeforeFirst}) OR startDate > lastDay ({StartAfterLast})",
                            evt.Id, evt.Title, startDate, endDate, endDate < firstDayDateTime, startDate > lastDayDateTime);
                        continue;
                    }

                    // Clamp start and end dates to the month range
                    var eventStartDate = startDate < firstDayOfMonth.ToDateTime(TimeOnly.MinValue) 
                        ? firstDayOfMonth.ToDateTime(TimeOnly.MinValue) 
                        : startDate;
                    var eventEndDate = endDate > lastDayOfMonth.ToDateTime(TimeOnly.MaxValue) 
                        ? lastDayOfMonth.ToDateTime(TimeOnly.MaxValue) 
                        : endDate;

                    // Calculate number of days for multi-day events using original dates
                    var numberOfDays = (endDate - startDate).Days + 1;
                    
                    // Use original start and end times from event
                    var appointmentStart = evt.StartTime;
                    var appointmentEnd = evt.EndTime;
                    
                    // For AllDay events, set to full day range but keep original end date
                    // This preserves the date range for calculating numOfDays correctly
                    if (evt.AllDay)
                    {
                        appointmentStart = eventStartDate.Date;
                        // Keep the original end date to preserve date range information
                        // Set time to end of day for the actual end date
                        appointmentEnd = new DateTime(endDate.Year, endDate.Month, endDate.Day, 23, 0, 0);
                    }

                    // Create single appointment with RecurrenceRule for multi-day events
                    var appointment = new Appointment
                    {
                        Id = evt.Id.ToString(),
                        Title = evt.Title ?? string.Empty,
                        Description = evt.Description ?? string.Empty,
                        Start = appointmentStart,
                        End = appointmentEnd,
                        AllDay = evt.AllDay
                    };

                    // Add RecurrenceRule for multi-day events
                    if (numberOfDays > 1)
                    {
                        appointment.RecurrenceRule = $"FREQ=DAILY;INTERVAL=1;COUNT={numberOfDays}";
                    }

                    testAppointments.Add(appointment);
                    
                    Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Added appointment - Id: {Id}, Title: {Title}, Start: {Start}, End: {End}, Days: {Days}, RecurrenceRule: {Rule}",
                        appointment.Id, appointment.Title, appointment.Start, appointment.End, numberOfDays, appointment.RecurrenceRule ?? "None");
                }
                
                Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Month View - Total appointments created: {Count}", testAppointments.Count);
            }

            Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - Before Update [RequestId: {RequestId}] - testAppointments Count: {Count}, SelectedSchedulerDate: {Date}", 
                requestId, testAppointments.Count, currentSelectedDate);
            
            // Update Appointments list using the public method from razor file
            await InvokeAsync(() =>
            {
                // Check if this is still the latest request (avoid race condition)
                if (requestId < _lastUpdateRequestId)
                {
                    Logger.LogWarning("UpdateTestAppointmentsFromCalendarEvents - Skipping update [RequestId: {RequestId}] - Newer request exists: {LatestRequestId}, SelectedSchedulerDate changed from {OldDate} to {NewDate}",
                        requestId, _lastUpdateRequestId, currentSelectedDate, SelectedSchedulerDate);
                    return;
                }
                
                // Verify SelectedSchedulerDate hasn't changed
                if (currentSelectedDate != SelectedSchedulerDate)
                {
                    Logger.LogWarning("UpdateTestAppointmentsFromCalendarEvents - Skipping update [RequestId: {RequestId}] - SelectedSchedulerDate changed from {OldDate} to {NewDate}",
                        requestId, currentSelectedDate, SelectedSchedulerDate);
                    return;
                }
                
                UpdateTestAppointments(testAppointments, requestId);
                Logger.LogInformation("UpdateTestAppointmentsFromCalendarEvents - After Update [RequestId: {RequestId}] - Appointments Count: {Count}", 
                    requestId, Appointments?.Count ?? 0);
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "UpdateTestAppointmentsFromCalendarEvents - Error: {Message}, StackTrace: {StackTrace}", ex.Message, ex.StackTrace);
        }
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetCalendarEventsAsync();
        await InvokeAsync(StateHasChanged);
    }

    // Load and cache Project codes for events with RelatedType = PROJECT
    // Helper method to get display code for RelatedId based on RelatedType
    // Both TASK and PROJECT now store Code in RelatedId, so just return it directly
    private string GetRelatedIdDisplayCode(CalendarEventDto calendarEvent)
    {
        if (string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
        {
            return string.Empty;
        }

        // Both TASK and PROJECT now store Code in RelatedId
        return calendarEvent.RelatedId;
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await CalendarEventsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/calendar-events/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Title={HttpUtility.UrlEncode(Filter.Title)}&Description={HttpUtility.UrlEncode(Filter.Description)}&StartTimeMin={Filter.StartTimeMin?.ToString("O")}&StartTimeMax={Filter.StartTimeMax?.ToString("O")}&EndTimeMin={Filter.EndTimeMin?.ToString("O")}&EndTimeMax={Filter.EndTimeMax?.ToString("O")}&AllDay={Filter.AllDay}&EventType={HttpUtility.UrlEncode(Filter.EventType?.ToString())}&Location={HttpUtility.UrlEncode(Filter.Location)}&RelatedType={HttpUtility.UrlEncode(Filter.RelatedType?.ToString())}&RelatedId={HttpUtility.UrlEncode(Filter.RelatedId)}&Visibility={HttpUtility.UrlEncode(Filter.Visibility?.ToString())}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<CalendarEventDto> e)
    {
        try
        {
            CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
            CurrentPage = e.Page;
            await GetCalendarEventsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OpenCreateCalendarEventModalAsync()
    {
        NewCalendarEvent = new CalendarEventCreateDto
        {
            StartTime = DateTime.Now,
            EndTime = DateTime.Now,
        };
        NewCalendarEventEventType = EventType.MEETING;
        NewCalendarEventRelatedType = RelatedType.NONE;
        NewCalendarEventVisibility = EventVisibility.PRIVATE;
        SelectedNewProject = new List<ProjectSelectItem>();
        SelectedNewProjectTask = new List<ProjectTaskSelectItem>();
        CreateFieldErrors.Clear();
        CreateCalendarEventValidationErrorKey = null;
        CreateGeneralValidationErrorKey = null;
        CreateWizardCalendarEventId = Guid.Empty;
        CreateParticipantsList = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
        CreateParticipantsUserToAdd = new List<LookupDto<Guid>>();
        CreateParticipantResponseStatus = ParticipantResponse.INVITED;
        SelectedCreateTab = "general";
        await CreateCalendarEventModal.Show();
    }

    private async Task CloseCreateCalendarEventModalAsync()
    {
        NewCalendarEvent = new CalendarEventCreateDto
        {
            StartTime = DateTime.Now,
            EndTime = DateTime.Now,
        };
        CreateWizardCalendarEventId = Guid.Empty;
        CreateGeneralValidationErrorKey = null;
        CreateFieldErrors.Clear();
        CreateParticipantsList = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
        CreateParticipantsUserToAdd = new List<LookupDto<Guid>>();
        SelectedCreateTab = "general";
        await CreateCalendarEventModal.Hide();
    }

    private async Task CancelCreateWizardAsync()
    {
        try
        {
            if (CreateWizardCalendarEventId != Guid.Empty)
            {
                if (!await UiMessageService.Confirm(L["CreateWizard:CancelAndDeleteEvent"].Value))
                {
                    return;
                }

                // Best-effort cleanup
                await CalendarEventsAppService.DeleteAsync(CreateWizardCalendarEventId);
            }

            await CloseCreateCalendarEventModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void OnSelectedCreateTabChanged(string name)
    {
        // Prevent switching to participants tab if general info is not saved
        if ((name == "participants") && !IsCreateWizardGeneralSaved)
        {
            SelectedCreateTab = "general";
            return;
        }

        SelectedCreateTab = name;
    }

    private async Task SaveGeneralInformationAsync()
    {
        try
        {
            if (IsCreateWizardGeneralSaved)
            {
                return;
            }

            if (!ValidateCreateGeneralInformation())
            {
                await UiMessageService.Warn(L[CreateGeneralValidationErrorKey ?? "ValidationError"]);
                await InvokeAsync(StateHasChanged);
                return;
            }

            NewCalendarEvent.EventType = NewCalendarEventEventType.ToString();
            NewCalendarEvent.RelatedType = NewCalendarEventRelatedType.ToString();
            NewCalendarEvent.Visibility = NewCalendarEventVisibility.ToString();

            // Set RelatedId based on RelatedType
            if (NewCalendarEventRelatedType == RelatedType.PROJECT)
            {
                NewCalendarEvent.RelatedId = SelectedNewProject.FirstOrDefault()?.Id;
            }
            else if (NewCalendarEventRelatedType == RelatedType.TASK)
            {
                NewCalendarEvent.RelatedId = SelectedNewProjectTask.FirstOrDefault()?.Id;
            }
            else
            {
                NewCalendarEvent.RelatedId = null;
            }

            var created = await CalendarEventsAppService.CreateAsync(NewCalendarEvent);
            CreateWizardCalendarEventId = created.Id;

            // Load participants after event is created
            await LoadCreateParticipantsAsync();

            SelectedCreateTab = "participants";
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool ValidateCreateGeneralInformation()
    {
        // Reset error state
        CreateGeneralValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: Title
        if (string.IsNullOrWhiteSpace(NewCalendarEvent.Title))
        {
            CreateFieldErrors["Title"] = L["TitleRequired"];
            CreateGeneralValidationErrorKey = "TitleRequired";
            isValid = false;
        }

        // Required: EventType
        if (NewCalendarEventEventType == default)
        {
            CreateFieldErrors["EventType"] = L["EventTypeRequired"];
            if (isValid)
            {
                CreateGeneralValidationErrorKey = "EventTypeRequired";
            }
            isValid = false;
        }

        // Required: RelatedId if RelatedType is not NONE
        if (NewCalendarEventRelatedType != RelatedType.NONE)
        {
            if (NewCalendarEventRelatedType == RelatedType.PROJECT && (SelectedNewProject == null || SelectedNewProject.Count == 0))
            {
                CreateFieldErrors["RelatedId"] = L["ProjectRequired"];
                if (isValid)
                {
                    CreateGeneralValidationErrorKey = "ProjectRequired";
                }
                isValid = false;
            }
            else if (NewCalendarEventRelatedType == RelatedType.TASK && (SelectedNewProjectTask == null || SelectedNewProjectTask.Count == 0))
            {
                CreateFieldErrors["RelatedId"] = L["ProjectTaskRequired"];
                if (isValid)
                {
                    CreateGeneralValidationErrorKey = "ProjectTaskRequired";
                }
                isValid = false;
            }
        }

        return isValid;
    }

    private async Task LoadCreateParticipantsAsync()
    {
        if (CreateWizardCalendarEventId == Guid.Empty)
        {
            CreateParticipantsList = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
            return;
        }

        var result = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
        {
            CalendarEventId = CreateWizardCalendarEventId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        CreateParticipantsList = result.Items;
    }

    protected async Task<List<LookupDto<Guid>>> GetParticipantIdentityUserLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await CalendarEventParticipantsAppService.GetIdentityUserLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        });

        ParticipantIdentityUsersCollection = result.Items;
        return result.Items.ToList();
    }

    protected void OnCreateParticipantUserChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task AddParticipantAsync()
    {
        try
        {
            if (!IsCreateWizardGeneralSaved)
            {
                await UiMessageService.Error(L["CreateWizard:SaveGeneralFirst"]);
                return;
            }

            var userId = CreateParticipantsUserToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                await UiMessageService.Error(L["CreateWizard:ParticipantRequired"]);
                return;
            }

            // Check if user is already added
            if (CreateParticipantsList.Any(p => p.CalendarEventParticipant.IdentityUserId == userId))
            {
                await UiMessageService.Warn(L["CreateWizard:ParticipantAlreadyAdded"]);
                return;
            }

            await CalendarEventParticipantsAppService.CreateAsync(new CalendarEventParticipantCreateDto
            {
                CalendarEventId = CreateWizardCalendarEventId,
                IdentityUserId = userId,
                ResponseStatus = CreateParticipantResponseStatus.ToString(),
                Notified = false
            });

            CreateParticipantsUserToAdd = new List<LookupDto<Guid>>();
            await LoadCreateParticipantsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteParticipantAsync(CalendarEventParticipantWithNavigationPropertiesDto row)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await CalendarEventParticipantsAppService.DeleteAsync(row.CalendarEventParticipant.Id);
            await LoadCreateParticipantsAsync();
            await InvokeAsync(StateHasChanged);
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

    private async Task FinishCreateWizardAsync()
    {
        try
        {
            await GetCalendarEventsAsync();
            await CloseCreateCalendarEventModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadEditParticipantsAsync()
    {
        if (EditingCalendarEventId == Guid.Empty)
        {
            EditParticipantsList = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
            return;
        }

        var result = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
        {
            CalendarEventId = EditingCalendarEventId,
            MaxResultCount = 1000,
            SkipCount = 0
        });

        EditParticipantsList = result.Items;
    }

    protected void OnEditParticipantUserChanged()
    {
        InvokeAsync(StateHasChanged);
    }

    private async Task AddEditParticipantAsync()
    {
        try
        {
            if (EditingCalendarEventId == Guid.Empty)
            {
                return;
            }

            var userId = EditParticipantsUserToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                await UiMessageService.Error(L["CreateWizard:ParticipantRequired"]);
                return;
            }

            // Check if user is already added
            if (EditParticipantsList.Any(p => p.CalendarEventParticipant.IdentityUserId == userId))
            {
                await UiMessageService.Warn(L["CreateWizard:ParticipantAlreadyAdded"]);
                return;
            }

            await CalendarEventParticipantsAppService.CreateAsync(new CalendarEventParticipantCreateDto
            {
                CalendarEventId = EditingCalendarEventId,
                IdentityUserId = userId,
                ResponseStatus = EditParticipantResponseStatus.ToString(),
                Notified = false
            });

            EditParticipantsUserToAdd = new List<LookupDto<Guid>>();
            await LoadEditParticipantsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteEditParticipantAsync(CalendarEventParticipantWithNavigationPropertiesDto row)
    {
        try
        {
            await CalendarEventParticipantsAppService.DeleteAsync(row.CalendarEventParticipant.Id);
            await LoadEditParticipantsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void OnAllDayChanged(bool allDay)
    {
        if (allDay)
        {
            // Set StartTime to 00:00:00
            var startDate = NewCalendarEvent.StartTime.Date;
            NewCalendarEvent.StartTime = startDate;
            
            // Set EndTime to 23:59:59
            var endDate = NewCalendarEvent.EndTime.Date;
            NewCalendarEvent.EndTime = endDate.AddDays(1).AddSeconds(-1);
        }
    }

    private void OnEditAllDayChanged(bool allDay)
    {
        if (allDay)
        {
            // Set StartTime to 00:00:00
            var startDate = EditingCalendarEvent.StartTime.Date;
            EditingCalendarEvent.StartTime = startDate;
            
            // Set EndTime to 23:59:59
            var endDate = EditingCalendarEvent.EndTime.Date;
            EditingCalendarEvent.EndTime = endDate.AddDays(1).AddSeconds(-1);
        }
    }

    // Dictionary to cache participant counts for each calendar event
    private Dictionary<Guid, int> _participantCountCache = new Dictionary<Guid, int>();

    private async Task NavigateToCalendarEventDetail(CalendarEventDto input)
    {
        NavigationManager.NavigateTo($"/calendar-event-detail/{input.Id}");
    }

    private async Task OpenEditCalendarEventModalAsync(CalendarEventDto input)
    {
        SelectedEditTab = "calendarEvent-edit-tab";
        var calendarEvent = await CalendarEventsAppService.GetAsync(input.Id);
        EditingCalendarEventId = calendarEvent.Id;
        EditingCalendarEvent = ObjectMapper.Map<CalendarEventDto, CalendarEventUpdateDto>(calendarEvent);
        
        // Parse enum values from string
        if (Enum.TryParse<EventType>(calendarEvent.EventType, out var eventType))
        {
            EditingCalendarEventEventType = eventType;
        }
        if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
        {
            EditingCalendarEventRelatedType = relatedType;
        }
        if (Enum.TryParse<EventVisibility>(calendarEvent.Visibility, out var visibility))
        {
            EditingCalendarEventVisibility = visibility;
        }

        // Set Select2 values
        SelectedEditProject = new List<ProjectSelectItem>();
        SelectedEditProjectTask = new List<ProjectTaskSelectItem>();
        if (!string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
        {
            if (EditingCalendarEventRelatedType == RelatedType.PROJECT)
            {
                await GetProjectCollectionLookupAsync();
                var project = ProjectsCollection.FirstOrDefault(p => p.Id == calendarEvent.RelatedId);
                if (project != null)
                {
                    SelectedEditProject = new List<ProjectSelectItem> { project };
                }
            }
            else if (EditingCalendarEventRelatedType == RelatedType.TASK)
            {
                await GetProjectTaskCollectionLookupAsync();
                var task = ProjectTasksCollection.FirstOrDefault(t => t.Id == calendarEvent.RelatedId);
                if (task != null)
                {
                    SelectedEditProjectTask = new List<ProjectTaskSelectItem> { task };
                }
            }
        }

        EditFieldErrors.Clear();
        EditCalendarEventValidationErrorKey = null;
        SelectedEditTab = "general";
        
        // Load participants
        await LoadEditParticipantsAsync();
        EditParticipantsUserToAdd = new List<LookupDto<Guid>>();
        EditParticipantResponseStatus = ParticipantResponse.INVITED;
        
        await EditCalendarEventModal.Show();
    }

    private async Task DeleteCalendarEventAsync(CalendarEventDto input)
    {
        try
        {
            await CalendarEventsAppService.DeleteAsync(input.Id);
            await GetCalendarEventsAsync();
            
            // Refresh scheduler if in Calendar view
            if (!IsListView)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteCalendarEventWithConfirmationAsync(CalendarEventDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteCalendarEventAsync(input);
        }
    }

    private async Task CreateCalendarEventAsync()
    {
        try
        {
            if (!ValidateCreateCalendarEvent())
            {
                await UiMessageService.Warn(L[CreateCalendarEventValidationErrorKey ?? "ValidationError"]);
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Set enum values as strings
            NewCalendarEvent.EventType = NewCalendarEventEventType.ToString();
            NewCalendarEvent.RelatedType = NewCalendarEventRelatedType.ToString();
            NewCalendarEvent.Visibility = NewCalendarEventVisibility.ToString();

            // Set RelatedId based on RelatedType
            if (NewCalendarEventRelatedType == RelatedType.PROJECT)
            {
                NewCalendarEvent.RelatedId = SelectedNewProject.FirstOrDefault()?.Id;
            }
            else if (NewCalendarEventRelatedType == RelatedType.TASK)
            {
                NewCalendarEvent.RelatedId = SelectedNewProjectTask.FirstOrDefault()?.Id;
            }
            else
            {
                NewCalendarEvent.RelatedId = null;
            }

            await CalendarEventsAppService.CreateAsync(NewCalendarEvent);
            await GetCalendarEventsAsync();
            await CloseCreateCalendarEventModalAsync();
            
            // Refresh scheduler if in Calendar view
            if (!IsListView)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditCalendarEventModalAsync()
    {
        await EditCalendarEventModal.Hide();
    }

    private async Task UpdateCalendarEventAsync()
    {
        try
        {
            if (!ValidateEditCalendarEvent())
            {
                await UiMessageService.Warn(L[EditCalendarEventValidationErrorKey ?? "ValidationError"]);
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Set enum values as strings
            EditingCalendarEvent.EventType = EditingCalendarEventEventType.ToString();
            EditingCalendarEvent.RelatedType = EditingCalendarEventRelatedType.ToString();
            EditingCalendarEvent.Visibility = EditingCalendarEventVisibility.ToString();

            // Set RelatedId based on RelatedType
            if (EditingCalendarEventRelatedType == RelatedType.PROJECT)
            {
                EditingCalendarEvent.RelatedId = SelectedEditProject.FirstOrDefault()?.Id;
            }
            else if (EditingCalendarEventRelatedType == RelatedType.TASK)
            {
                EditingCalendarEvent.RelatedId = SelectedEditProjectTask.FirstOrDefault()?.Id;
            }
            else
            {
                EditingCalendarEvent.RelatedId = null;
            }

            await CalendarEventsAppService.UpdateAsync(EditingCalendarEventId, EditingCalendarEvent);
            await GetCalendarEventsAsync();
            await EditCalendarEventModal.Hide();
            
            // Refresh scheduler if in Calendar view
            if (!IsListView)
            {
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;
    }

    protected virtual async Task OnTitleChangedAsync(string? title)
    {
        Filter.Title = title;
        await SearchAsync();
    }

    protected virtual async Task OnDescriptionChangedAsync(string? description)
    {
        Filter.Description = description;
        await SearchAsync();
    }

    protected virtual async Task OnStartTimeMinChangedAsync(DateTime? startTimeMin)
    {
        Filter.StartTimeMin = startTimeMin.HasValue ? startTimeMin.Value.Date : startTimeMin;
        await SearchAsync();
    }

    protected virtual async Task OnStartTimeMaxChangedAsync(DateTime? startTimeMax)
    {
        Filter.StartTimeMax = startTimeMax.HasValue ? startTimeMax.Value.Date.AddDays(1).AddSeconds(-1) : startTimeMax;
        await SearchAsync();
    }

    protected virtual async Task OnEndTimeMinChangedAsync(DateTime? endTimeMin)
    {
        Filter.EndTimeMin = endTimeMin.HasValue ? endTimeMin.Value.Date : endTimeMin;
        await SearchAsync();
    }

    protected virtual async Task OnEndTimeMaxChangedAsync(DateTime? endTimeMax)
    {
        Filter.EndTimeMax = endTimeMax.HasValue ? endTimeMax.Value.Date.AddDays(1).AddSeconds(-1) : endTimeMax;
        await SearchAsync();
    }

    protected virtual async Task OnAllDayChangedAsync(bool? allDay)
    {
        Filter.AllDay = allDay;
        await SearchAsync();
    }

    protected virtual async Task OnFilterEventTypeChangedAsync(EventType? eventType)
    {
        FilterEventType = eventType;
        Filter.EventType = eventType?.ToString();
        await SearchAsync();
    }

    protected virtual async Task OnLocationChangedAsync(string? location)
    {
        Filter.Location = location;
        await SearchAsync();
    }

    protected virtual async Task OnFilterRelatedTypeChangedAsync(RelatedType? relatedType)
    {
        //clear Filter.RelatedId nếu relatedType khác với Filter.RelatedType
        if (relatedType.ToString() != Filter.RelatedType)
        {
            Filter.RelatedId = null;
            SelectedFilterProject.Clear();
            SelectedFilterProjectTask.Clear();
        }

        FilterRelatedType = relatedType;
        Filter.RelatedType = relatedType?.ToString();

        await SearchAsync();
    }

    protected virtual async Task OnFilterRelatedIdChangedAsync()
    {
        if (FilterRelatedType == RelatedType.PROJECT)
        {
            Filter.RelatedId = SelectedFilterProject?.FirstOrDefault()?.Id;
        }
        else if (FilterRelatedType == RelatedType.TASK)
        {
            Filter.RelatedId = SelectedFilterProjectTask?.FirstOrDefault()?.Id;
        }
        else
        {
            Filter.RelatedId = null;
        }
        await SearchAsync();
    }

    protected virtual async Task OnRelatedIdChangedAsync(string? relatedId)
    {
        Filter.RelatedId = relatedId;
        await SearchAsync();
    }

    protected virtual async Task OnFilterVisibilityChangedAsync(EventVisibility? visibility)
    {
        FilterVisibility = visibility;
        Filter.Visibility = visibility?.ToString();
        await SearchAsync();
    }

    private Task SelectAllItems()
    {
        AllCalendarEventsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllCalendarEventsSelected = false;
        SelectedCalendarEvents.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedCalendarEventRowsChanged()
    {
        if (SelectedCalendarEvents.Count != PageSize)
        {
            AllCalendarEventsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedCalendarEventsAsync()
    {
        var message = AllCalendarEventsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedCalendarEvents.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllCalendarEventsSelected)
        {
            await CalendarEventsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await CalendarEventsAppService.DeleteByIdsAsync(SelectedCalendarEvents.Select(x => x.Id).ToList());
        }

        SelectedCalendarEvents.Clear();
        AllCalendarEventsSelected = false;
        await GetCalendarEventsAsync();
        
        // Refresh scheduler if in Calendar view
        if (!IsListView)
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    // Validation methods
    private bool ValidateCreateCalendarEvent()
    {
        CreateCalendarEventValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: Title
        if (string.IsNullOrWhiteSpace(NewCalendarEvent?.Title))
        {
            CreateFieldErrors["Title"] = L["TitleRequired"];
            CreateCalendarEventValidationErrorKey = "TitleRequired";
            isValid = false;
        }

        if (NewCalendarEventRelatedType == RelatedType.PROJECT)
        {
            if (SelectedNewProject == null || SelectedNewProject.Count == 0)
            {
                CreateFieldErrors["RelatedId"] = L["ProjectRequired"];
                if (isValid)
                {
                    CreateCalendarEventValidationErrorKey = "ProjectRequired";
                }
                isValid = false;
            }
        }
        else if (NewCalendarEventRelatedType == RelatedType.TASK)
        {
            if (SelectedNewProjectTask == null || SelectedNewProjectTask.Count == 0)
            {
                CreateFieldErrors["RelatedId"] = L["ProjectTaskRequired"];
                if (isValid)
                {
                    CreateCalendarEventValidationErrorKey = "ProjectTaskRequired";
                }
                isValid = false;
            }
        }

        // Required: Visibility
        // Visibility is enum, already set

        return isValid;
    }

    private bool ValidateEditCalendarEvent()
    {
        EditCalendarEventValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        // Required: Title
        if (string.IsNullOrWhiteSpace(EditingCalendarEvent?.Title))
        {
            EditFieldErrors["Title"] = L["TitleRequired"];
            EditCalendarEventValidationErrorKey = "TitleRequired";
            isValid = false;
        }

        if (EditingCalendarEventRelatedType == RelatedType.PROJECT)
        {
            if (SelectedEditProject == null || SelectedEditProject.Count == 0)
            {
                EditFieldErrors["RelatedId"] = L["ProjectRequired"];
                if (isValid)
                {
                    EditCalendarEventValidationErrorKey = "ProjectRequired";
                }
                isValid = false;
            }
        }
        else if (EditingCalendarEventRelatedType == RelatedType.TASK)
        {
            if (SelectedEditProjectTask == null || SelectedEditProjectTask.Count == 0)
            {
                EditFieldErrors["RelatedId"] = L["ProjectTaskRequired"];
                if (isValid)
                {
                    EditCalendarEventValidationErrorKey = "ProjectTaskRequired";
                }
                isValid = false;
            }
        }

        // Required: Visibility
        // Visibility is enum, already set

        return isValid;
    }

    // Lookup methods
    private async Task GetProjectCollectionLookupAsync(string? newValue = null)
    {
        var input = new GetProjectsInput
        {
            FilterText = newValue,
            MaxResultCount = 20,
            SkipCount = 0,
        };

        var result = await ProjectsAppService.GetListAsync(input);
        ProjectsCollection = result.Items
            .Where(x => x.Project != null && !string.IsNullOrWhiteSpace(x.Project.Code))
            .Select(x => new ProjectSelectItem
            {
                Id = x.Project.Code,
                DisplayName = $"{x.Project.Code} - {x.Project.Name}",
            })
            .ToList();
    }

    private async Task<List<ProjectSelectItem>> GetProjectCollectionLookupAsync(IReadOnlyList<ProjectSelectItem> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectsInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
        };

        var result = await ProjectsAppService.GetListAsync(input);
        ProjectsCollection = result.Items
            .Where(x => x.Project != null && !string.IsNullOrWhiteSpace(x.Project.Code))
            .Select(x => new ProjectSelectItem
            {
                Id = x.Project.Code,
                DisplayName = $"{x.Project.Code} - {x.Project.Name}",
            })
            .ToList();
        return ProjectsCollection.ToList();
    }

    private async Task GetProjectTaskCollectionLookupAsync(string? newValue = null)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = newValue,
            MaxResultCount = 20,
            SkipCount = 0,
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        ProjectTasksCollection = result.Items
            .Select(x => new ProjectTaskSelectItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}",
            })
            .ToList();
    }

    private async Task<List<ProjectTaskSelectItem>> GetProjectTaskCollectionLookupAsync(IReadOnlyList<ProjectTaskSelectItem> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        ProjectTasksCollection = result.Items
            .Select(x => new ProjectTaskSelectItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}",
            })
            .ToList();

        return ProjectTasksCollection.ToList();
    }

    // Select2 change handlers
    protected void OnNewProjectChanged()
    {
        CreateFieldErrors.Remove("RelatedId");
    }

    protected void OnNewProjectTaskChanged()
    {
        CreateFieldErrors.Remove("RelatedId");
    }

    protected void OnEditProjectChanged()
    {
        EditFieldErrors.Remove("RelatedId");
    }

    protected void OnEditProjectTaskChanged()
    {
        EditFieldErrors.Remove("RelatedId");
    }

    private async Task OnSchedulerItemInserted(SchedulerInsertedItem<Appointment> item)
    {
        try
        {
            CalendarEventDto? originalEvent = null;
            originalEvent = CalendarEventList.FirstOrDefault(e => e.Id == Guid.Parse(item.Item.Id));

            NewCalendarEvent = new CalendarEventCreateDto
            {
                Title = item.Item.Title,
                Description = item.Item.Description ?? string.Empty,
                StartTime = item.Item.Start,
                EndTime = item.Item.End,
                AllDay = item.Item.AllDay,
                EventType = originalEvent?.EventType ?? EventType.MEETING.ToString(),
                RelatedType = originalEvent?.RelatedType ?? RelatedType.NONE.ToString(),
                Visibility = originalEvent?.Visibility ?? EventVisibility.PRIVATE.ToString()
            };
            
            if (originalEvent != null && Enum.TryParse<EventType>(originalEvent.EventType, out var eventType))
            {
                NewCalendarEventEventType = eventType;
            }
            else
            {
                NewCalendarEventEventType = EventType.MEETING;
            }
            
            if (originalEvent != null && Enum.TryParse<RelatedType>(originalEvent.RelatedType, out var relatedType))
            {
                NewCalendarEventRelatedType = relatedType;
            }
            else
            {
                NewCalendarEventRelatedType = RelatedType.NONE;
            }
            
            if (originalEvent != null && Enum.TryParse<EventVisibility>(originalEvent.Visibility, out var visibility))
            {
                NewCalendarEventVisibility = visibility;
            }
            else
            {
                NewCalendarEventVisibility = EventVisibility.PRIVATE;
            }
            
            SelectedNewProject = new List<ProjectSelectItem>();
            SelectedNewProjectTask = new List<ProjectTaskSelectItem>();
            CreateFieldErrors.Clear();
            CreateCalendarEventValidationErrorKey = null;
            
            await CreateCalendarEventModal.Show();
            RebuildToolbar();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OnSchedulerItemClicked(SchedulerItemClickedEventArgs<Appointment> args)
    {
        try
        {
            
            Logger.LogInformation("OnSchedulerItemClicked - Start - Item Id: {Id}", args.Item.Id);
            var calendarEvent = CalendarEventList.FirstOrDefault(e => e.Id == Guid.Parse(args.Item.Id));
            if (calendarEvent == null)
            {
                calendarEvent = await CalendarEventsAppService.GetAsync(Guid.Parse(args.Item.Id));
            }
            
            if (Enum.TryParse<RelatedType>(calendarEvent.RelatedType, out var relatedType))
            {
                if (relatedType == RelatedType.PROJECT && !string.IsNullOrWhiteSpace(calendarEvent.RelatedId))
                {
                    if (Guid.TryParse(calendarEvent.RelatedId, out var projectId))
                    {
                        NavigationManager.NavigateTo($"/project-detail/{projectId}");
                        return;
                    }
                }
                else if (relatedType == RelatedType.TASK)
                {
                    return;
                }
            }
            
            await OpenEditCalendarEventModalAsync(calendarEvent);
            
            RebuildToolbar();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }
}