using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Web;
using System.Threading;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.CalendarEvents;
using HC.CalendarEventParticipants;
using HC.ProjectTasks;
using HC.Projects;
using HC.ProjectMembers;
using HC.ProjectTaskAssignments;
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
using Volo.Abp.AspNetCore.Components.Messages;

namespace HC.Blazor.Pages;


public partial class CalendarEvents : HCComponentBase, IAsyncDisposable
{
    [Inject] private ICalendarEventParticipantsAppService CalendarEventParticipantsAppService { get; set; } = default!;
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;
    [Inject] private ILogger<CalendarEvents> Logger { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; set; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    // View toggle
    protected bool IsListView { get; set; } = false;

    public DataGrid<CalendarEventDto> DataGridRef { get; set; }

    private IReadOnlyList<CalendarEventDto> CalendarEventList { get; set; }
  
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

    // Filter Select values (string for reliable "All" selection - same pattern as Projects)
    private string FilterEventTypeValue { get; set; } = string.Empty;
    private string FilterRelatedTypeValue { get; set; } = string.Empty;
    private string FilterVisibilityValue { get; set; } = string.Empty;
    private string FilterAllDayValue { get; set; } = string.Empty;

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


    private const string FullCalendarMonthView = "dayGridMonth";
    private const string FullCalendarElementId = "hc-calendar-events-fullcalendar";

    // Calendar properties
    private DateOnly SelectedSchedulerDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    private string SelectedSchedulerView { get; set; } = FullCalendarMonthView;
    private DateTime? CalendarRangeStart { get; set; }
    private DateTime? CalendarRangeEndExclusive { get; set; }
    private bool CalendarSyncRequired { get; set; }
    private bool CalendarIsInitialized { get; set; }
    private DotNetObjectReference<CalendarEvents>? CalendarDotNetRef { get; set; }
    private List<Appointment> Appointments { get; set; } = new();

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
        EnsureSelectedCalendarDate();

        // Initialize filter Select values from Filter (same pattern as Projects)
        FilterEventTypeValue = Filter.EventType ?? string.Empty;
        FilterRelatedTypeValue = Filter.RelatedType ?? string.Empty;
        FilterVisibilityValue = Filter.Visibility ?? string.Empty;
        FilterAllDayValue = Filter.AllDay.HasValue ? Filter.AllDay.Value.ToString() : string.Empty;
        
        await SetPermissionsAsync();
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

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            // Events are loaded in OnInitializedAsync (calendar view). List view loads via ReadData -> OnDataGridReadAsync.
        }

        if (IsListView)
        {
            if (CalendarIsInitialized)
            {
                await DestroyFullCalendarAsync();
            }

            return;
        }

        if (CalendarSyncRequired)
        {
            await SyncFullCalendarAsync();
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
        EnsureSelectedCalendarDate();
        
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
                var (rangeStart, rangeEndExclusive) = GetCalendarVisibleRange();
                var rangeEndInclusive = rangeEndExclusive.AddTicks(-1);

                calendarFilter = new GetCalendarEventsInput
                {
                    MaxResultCount = 200,
                    SkipCount = 0,
                    Sorting = CurrentSorting,
                    StartTimeMax = rangeEndInclusive,
                    EndTimeMin = rangeStart
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
                    // Apply date filters: intersect user's date range with calendar visible range
                    calendarFilter.StartTimeMin = Filter.StartTimeMin;
                    calendarFilter.EndTimeMax = Filter.EndTimeMax;
                    calendarFilter.StartTimeMax = Filter.StartTimeMax.HasValue && Filter.StartTimeMax.Value < rangeEndInclusive
                        ? Filter.StartTimeMax.Value
                        : rangeEndInclusive;
                    calendarFilter.EndTimeMin = Filter.EndTimeMin.HasValue && Filter.EndTimeMin.Value > rangeStart
                        ? Filter.EndTimeMin.Value
                        : rangeStart;
                }

                Logger.LogInformation(
                    "GetCalendarEventsAsync - IsListView: {IsList}, SelectedSchedulerView: {View}, SelectedSchedulerDate: {Date}, StartDate: {StartDate}, EndDateExclusive: {EndDateExclusive}",
                    IsListView,
                    SelectedSchedulerView,
                    SelectedSchedulerDate,
                    rangeStart,
                    rangeEndExclusive);
            }
            

            var result = await CalendarEventsAppService.GetListAsync(calendarFilter);
            CalendarEventList = result.Items ?? new List<CalendarEventDto>();
            TotalCount = (int)(result?.TotalCount ?? 0);
            
            Logger.LogInformation("GetCalendarEventsAsync - API Result - TotalCount: {Total}, Items Count: {Items}, CalendarEventList Count: {ListCount}",
                TotalCount, result?.Items?.Count ?? 0, CalendarEventList.Count);
            
            // Pre-load participant counts for all events (single batch API + SQL GROUP BY)
            if (CalendarEventList.Any())
            {
                var eventIds = CalendarEventList.Select(e => e.Id).ToList();
                try
                {
                    var countItems = await CalendarEventParticipantsAppService.CalculateParticipantCountsByCalendarEventIdsAsync(
                        new GetCalendarEventParticipantCountsInput { CalendarEventIds = eventIds });
                    foreach (var row in countItems)
                    {
                        _participantCountCache[row.CalendarEventId] = row.Count;
                    }
                }
                catch
                {
                    foreach (var id in eventIds)
                    {
                        _participantCountCache[id] = 0;
                    }
                }
            }

            await ClearSelection();
            
            await UpdateTestAppointmentsFromCalendarEvents();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            CalendarEventList = new List<CalendarEventDto>();
            Appointments.Clear();
            CalendarSyncRequired = true;
            TotalCount = 0;
        }
    }

    private async Task UpdateTestAppointmentsFromCalendarEvents()
    {
        // Generate unique request ID for this update
        var requestId = Interlocked.Increment(ref _lastUpdateRequestId);
        var currentSelectedDate = SelectedSchedulerDate;
        var currentView = SelectedSchedulerView;
        
        try
        {
            if (CalendarEventList == null || !CalendarEventList.Any())
            {
                Logger.LogWarning("UpdateTestAppointmentsFromCalendarEvents - CalendarEventList is null or empty");
                await InvokeAsync(() => UpdateTestAppointments(new List<Appointment>(), requestId));
                return;
            }

            var testAppointments = CalendarEventList
                .Select(evt =>
                {
                    var appointmentEnd = evt.AllDay
                        ? evt.EndTime.Date.AddDays(1)
                        : evt.EndTime;

                    return new Appointment
                    {
                        Id = evt.Id.ToString(),
                        CalendarEventId = evt.Id,
                        Title = evt.Title ?? string.Empty,
                        Description = evt.Description ?? string.Empty,
                        Start = evt.StartTime,
                        End = appointmentEnd,
                        AllDay = evt.AllDay,
                        CssClass = GetCalendarEventCssClass(evt)
                    };
                })
                .ToList();

            Logger.LogInformation(
                "UpdateTestAppointmentsFromCalendarEvents - Prepared {Count} appointments for FullCalendar",
                testAppointments.Count);

            await InvokeAsync(() =>
            {
                if (requestId < _lastUpdateRequestId)
                {
                    Logger.LogWarning(
                        "UpdateTestAppointmentsFromCalendarEvents - Skipping update [RequestId: {RequestId}] because a newer request exists",
                        requestId);
                    return;
                }

                if (currentSelectedDate != SelectedSchedulerDate)
                {
                    Logger.LogWarning(
                        "UpdateTestAppointmentsFromCalendarEvents - Skipping update [RequestId: {RequestId}] because SelectedSchedulerDate changed from {OldDate} to {NewDate}",
                        requestId, currentSelectedDate, SelectedSchedulerDate);
                    return;
                }
                
                UpdateTestAppointments(testAppointments, requestId);
                Logger.LogInformation(
                    "UpdateTestAppointmentsFromCalendarEvents - After Update [RequestId: {RequestId}] - Appointments Count: {Count}",
                    requestId,
                    Appointments.Count);
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
        return calendarEvent.RelatedId;
    }

    /// <summary>
    /// Get related entity link info for display in list. Uses RelatedName and RelatedEntityId from DTO (populated by backend).
    /// </summary>
    private (string DisplayName, string Url, IconName Icon)? GetRelatedEntityLinkInfo(CalendarEventDto calendarEvent)
    {
        if (!calendarEvent.RelatedEntityId.HasValue || string.IsNullOrWhiteSpace(calendarEvent.RelatedName))
        {
            return null;
        }

        if (calendarEvent.RelatedType == RelatedType.PROJECT.ToString())
        {
            return (calendarEvent.RelatedName, $"/project-detail/{calendarEvent.RelatedEntityId}", IconName.Folder);
        }
        if (calendarEvent.RelatedType == RelatedType.TASK.ToString())
        {
            return (calendarEvent.RelatedName, $"/project-task-detail/{calendarEvent.RelatedEntityId}", IconName.CheckSquare);
        }
        return null;
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
                await UiMessageService.Warn(L[CreateGeneralValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
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
            MaxResultCount = 200,
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
                await UiMessageService.Error(L["CreateWizard:SaveGeneralFirst"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var userId = CreateParticipantsUserToAdd.FirstOrDefault()?.Id ?? Guid.Empty;
            if (userId == Guid.Empty)
            {
                await UiMessageService.Error(L["CreateWizard:ParticipantRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Check if user is already added
            if (CreateParticipantsList.Any(p => p.CalendarEventParticipant.IdentityUserId == userId))
            {
                await UiMessageService.Warn(L["CreateWizard:ParticipantAlreadyAdded"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
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
            MaxResultCount = 200,
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
                await UiMessageService.Error(L["CreateWizard:ParticipantRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Check if user is already added
            if (EditParticipantsList.Any(p => p.CalendarEventParticipant.IdentityUserId == userId))
            {
                await UiMessageService.Warn(L["CreateWizard:ParticipantAlreadyAdded"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
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
            Guid relatedId = Guid.Empty;
            relatedId = Guid.TryParse(calendarEvent.RelatedId, out  relatedId) ? relatedId : Guid.Empty;
            if (EditingCalendarEventRelatedType == RelatedType.PROJECT && relatedId != Guid.Empty)
            {
                var project = await ProjectsAppService.GetAsync(relatedId);
                if (project != null)
                {
                    SelectedEditProject = new List<ProjectSelectItem> { new ProjectSelectItem { Id = project.Code, DisplayName = $"{project.Code} - {project.Name}" } };
                }
            }
            else if (EditingCalendarEventRelatedType == RelatedType.TASK && relatedId != Guid.Empty)
            {
                var task = await ProjectTasksAppService.GetAsync(relatedId);
                if (task != null)
                {
                    SelectedEditProjectTask = new List<ProjectTaskSelectItem> { new ProjectTaskSelectItem { Id = task.Code, DisplayName = $"{task.Code} - {task.Title}" } };
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
                await UiMessageService.Warn(L[CreateCalendarEventValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
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
                await UiMessageService.Warn(L[EditCalendarEventValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
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

    protected virtual async Task OnAllDayChangedAsync(string? allDayValue)
    {
        FilterAllDayValue = allDayValue ?? string.Empty;
        Filter.AllDay = bool.TryParse(allDayValue, out var parsed) ? parsed : null;
        await SearchAsync();
    }

    protected virtual async Task OnFilterEventTypeChangedAsync(string? eventTypeValue)
    {
        FilterEventTypeValue = eventTypeValue ?? string.Empty;
        Filter.EventType = Enum.TryParse<EventType>(eventTypeValue, true, out var parsed) ? parsed.ToString() : null;
        await SearchAsync();
    }

    protected virtual async Task OnLocationChangedAsync(string? location)
    {
        Filter.Location = location;
        await SearchAsync();
    }

    protected virtual async Task OnFilterRelatedTypeChangedAsync(string? relatedTypeValue)
    {
        // Clear Filter.RelatedId when relatedType differs from Filter.RelatedType
        if (relatedTypeValue != Filter.RelatedType)
        {
            Filter.RelatedId = null;
            SelectedFilterProject.Clear();
            SelectedFilterProjectTask.Clear();
        }

        FilterRelatedTypeValue = relatedTypeValue ?? string.Empty;
        Filter.RelatedType = Enum.TryParse<RelatedType>(relatedTypeValue, true, out var parsed) ? parsed.ToString() : null;

        await SearchAsync();
    }

    protected virtual async Task OnFilterRelatedIdChangedAsync()
    {
        if (Enum.TryParse<RelatedType>(FilterRelatedTypeValue, true, out var parsedRelatedType))
        {
            if (parsedRelatedType == RelatedType.PROJECT)
            {
                Filter.RelatedId = SelectedFilterProject?.FirstOrDefault()?.Id;
            }
            else if (parsedRelatedType == RelatedType.TASK)
            {
                Filter.RelatedId = SelectedFilterProjectTask?.FirstOrDefault()?.Id;
            }
            else
            {
                Filter.RelatedId = null;
            }
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

    protected virtual async Task OnFilterVisibilityChangedAsync(string? visibilityValue)
    {
        FilterVisibilityValue = visibilityValue ?? string.Empty;
        Filter.Visibility = Enum.TryParse<EventVisibility>(visibilityValue, true, out var parsed) ? parsed.ToString() : null;
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

    [JSInvokable]
    public async Task HandleCalendarDateClick(string clickedDate)
    {
        try
        {
            await OnSchedulerDayClicked(ParseFullCalendarDate(clickedDate));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    [JSInvokable]
    public async Task HandleCalendarEventClick(string calendarEventId)
    {
        if (!Guid.TryParse(calendarEventId, out var parsedId))
        {
            return;
        }

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            var calendarEvent = CalendarEventList.FirstOrDefault(e => e.Id == parsedId)
                ?? await CalendarEventsAppService.GetAsync(parsedId);

            await NavigateToRelatedEntity(calendarEvent);
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

    [JSInvokable]
    public async Task HandleCalendarDatesSet(string start, string endExclusive, string currentDate, string viewType)
    {
        try
        {
            var parsedStart = ParseFullCalendarDate(start);
            var parsedEndExclusive = ParseFullCalendarDate(endExclusive);
            var parsedCurrentDate = ParseFullCalendarDate(currentDate);
            var normalizedView = NormalizeFullCalendarView(viewType);
            var normalizedDate = DateOnly.FromDateTime(parsedCurrentDate);

            var hasChanged =
                CalendarRangeStart != parsedStart
                || CalendarRangeEndExclusive != parsedEndExclusive
                || !string.Equals(SelectedSchedulerView, normalizedView, StringComparison.Ordinal)
                || SelectedSchedulerDate != normalizedDate;

            if (!hasChanged)
            {
                return;
            }

            CalendarRangeStart = parsedStart;
            CalendarRangeEndExclusive = parsedEndExclusive;
            SelectedSchedulerView = normalizedView;
            SelectedSchedulerDate = normalizedDate;

            await GetCalendarEventsAsync();
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private void UpdateTestAppointments(List<Appointment> newAppointments, long requestId = 0)
    {
        Logger.LogInformation(
            "UpdateTestAppointments - Start [RequestId: {RequestId}] - Input appointments count: {Count}, Current Appointments count: {CurrentCount}",
            requestId,
            newAppointments?.Count ?? 0,
            Appointments.Count);

        Appointments = newAppointments ?? new List<Appointment>();
        CalendarSyncRequired = true;
        StateHasChanged();
    }

    private void EnsureSelectedCalendarDate()
    {
        var firstDayOfMonth = new DateOnly(SelectedSchedulerDate.Year, SelectedSchedulerDate.Month, 1);
        if (SelectedSchedulerDate != firstDayOfMonth)
        {
            SelectedSchedulerDate = firstDayOfMonth;
        }
    }

    private (DateTime Start, DateTime EndExclusive) GetCalendarVisibleRange()
    {
        if (CalendarRangeStart.HasValue && CalendarRangeEndExclusive.HasValue)
        {
            return (CalendarRangeStart.Value, CalendarRangeEndExclusive.Value);
        }

        var selectedDate = SelectedSchedulerDate.ToDateTime(TimeOnly.MinValue);

        return (new DateTime(selectedDate.Year, selectedDate.Month, 1), new DateTime(selectedDate.Year, selectedDate.Month, 1).AddMonths(1));
    }

    private async Task SyncFullCalendarAsync()
    {
        CalendarDotNetRef ??= DotNetObjectReference.Create(this);

        await JSRuntime.InvokeVoidAsync(
            "hcCalendarEvents.render",
            FullCalendarElementId,
            new
            {
                initialDate = SelectedSchedulerDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                initialView = NormalizeFullCalendarView(SelectedSchedulerView),
                locale = "vi",
                buttonText = new
                {
                    today = L["Today"].Value,
                    month = L["Month"].Value
                },
                events = Appointments.Select(appointment => new
                {
                    id = appointment.Id,
                    title = appointment.Title,
                    start = appointment.Start,
                    end = appointment.End,
                    allDay = appointment.AllDay,
                    classNames = new[] { appointment.CssClass },
                    extendedProps = new
                    {
                        calendarEventId = appointment.CalendarEventId.ToString(),
                        description = appointment.Description
                    }
                }).ToList()
            },
            CalendarDotNetRef);

        CalendarIsInitialized = true;
        CalendarSyncRequired = false;
    }

    private async Task DestroyFullCalendarAsync()
    {
        try
        {
            await JSRuntime.InvokeVoidAsync("hcCalendarEvents.destroy", FullCalendarElementId);
        }
        catch (JSDisconnectedException)
        {
            // Ignore disconnects while tearing down the circuit.
        }

        CalendarIsInitialized = false;
        CalendarSyncRequired = false;
    }

    private static string NormalizeFullCalendarView(string? _)
    {
        // UI exposes month grid only; ignore any legacy/other view types from the calendar.
        return FullCalendarMonthView;
    }

    private static DateTime ParseFullCalendarDate(string value)
    {
        if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var dateTimeOffset))
        {
            return dateTimeOffset.LocalDateTime;
        }

        return DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal);
    }

    private static string GetCalendarEventCssClass(CalendarEventDto calendarEvent)
    {
        if (string.Equals(calendarEvent.RelatedType, nameof(RelatedType.TASK), StringComparison.OrdinalIgnoreCase))
        {
            return "hc-calendar-event-brand";
        }

        if (string.Equals(calendarEvent.RelatedType, nameof(RelatedType.PROJECT), StringComparison.OrdinalIgnoreCase))
        {
            return "hc-calendar-event-primary";
        }

        if (!Enum.TryParse<EventType>(calendarEvent.EventType, out var eventType))
        {
            return string.Equals(calendarEvent.Visibility, EventVisibility.PUBLIC.ToString(), StringComparison.OrdinalIgnoreCase)
                ? "hc-calendar-event-primary"
                : "hc-calendar-event-secondary";
        }

        return eventType switch
        {
            EventType.DEADLINE or EventType.TASK_DUE_SOON or EventType.TASK_ASSIGN_REMOVED or EventType.PROJECT_MEMBER_REMOVED => "hc-calendar-event-danger",
            EventType.REMINDER or EventType.CALENDAR_REMINDER => "hc-calendar-event-warning",
            EventType.WORKFLOW_COMPLETED => "hc-calendar-event-success",
            EventType.WORKFLOW_ASSIGNED or EventType.TASK_ASSIGNED or EventType.TASK_ASSIGN_UPDATED or EventType.PROJECT_MEMBER_ADDED or EventType.PROJECT_MEMBER_UPDATED or EventType.CALENDAR_INVITED => "hc-calendar-event-info",
            _ => string.Equals(calendarEvent.Visibility, EventVisibility.PUBLIC.ToString(), StringComparison.OrdinalIgnoreCase)
                ? "hc-calendar-event-primary"
                : "hc-calendar-event-secondary"
        };
    }

    public async ValueTask DisposeAsync()
    {
        if (CalendarIsInitialized)
        {
            await DestroyFullCalendarAsync();
        }

        CalendarDotNetRef?.Dispose();
        CalendarDotNetRef = null;
    }

    public sealed class Appointment
    {
        public string Id { get; set; } = string.Empty;
        public Guid CalendarEventId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Start { get; set; }
        public DateTime End { get; set; }
        public bool AllDay { get; set; }
        public string CssClass { get; set; } = "hc-calendar-event-secondary";
    }
}