using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using HC.Permissions;
using HC.CalendarEventParticipants;
using HC.CalendarEvents;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Blazor.Shared;
using HC.Projects;
using HC.ProjectTasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;

public partial class CalendarEventDetail : HCComponentBase
{
    // Accept route param and query param (?id=...).
    [Parameter] public Guid CalendarEventId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? CalendarEventIdQuery { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; set; } = new();

    protected PageToolbar Toolbar { get; } = new PageToolbar();

    protected string PageTitle
    {
        get
        {
            if (CalendarEventId == Guid.Empty)
                return L["NewCalendarEvent"];
            return CurrentCalendarEvent is null
                ? L["CalendarEvents"]
                : $"{CurrentCalendarEvent.Title}";
        }
    }

    protected bool IsLoadingCalendarEvent { get; set; }
    protected CalendarEventDto? CurrentCalendarEvent { get; set; }

    // Create/Edit CalendarEvent properties
    private CalendarEventCreateDto NewCalendarEvent { get; set; }
    private CalendarEventUpdateDto EditingCalendarEvent { get; set; }

    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Validation error keys
    private string? CreateCalendarEventValidationErrorKey { get; set; }
    private string? EditCalendarEventValidationErrorKey { get; set; }

    // Date pickers
    private DatePicker<DateTime>? NewCalendarEventStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewCalendarEventEndDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditingCalendarEventStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditingCalendarEventEndDateDatePicker { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;

    // Participants tab
    public DataGrid<CalendarEventParticipantWithNavigationPropertiesDto>? ParticipantsDataGridRef { get; set; }
    private IReadOnlyList<CalendarEventParticipantWithNavigationPropertiesDto> ParticipantsList { get; set; } = new List<CalendarEventParticipantWithNavigationPropertiesDto>();
    private int ParticipantsTotalCount { get; set; }
    private int ParticipantsCurrentPage { get; set; } = 1;
    private string ParticipantsSorting { get; set; } = string.Empty;
    private string? ParticipantsFilterText { get; set; }

    // Participant add/edit role UI
    private bool CanCreateCalendarEventParticipant { get; set; }
    private bool CanDeleteCalendarEventParticipant { get; set; }
    private bool CanEditCalendarEventParticipant { get; set; }


    private bool CanCreateCalendarEvent { get; set; }
    private bool CanEditCalendarEvent { get; set; }
    private bool CanDeleteCalendarEvent { get; set; }

    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> ParticipantsToAdd { get; set; } = new();
    private bool IsParticipantResponseEditMode { get; set; }
    private Guid EditingParticipantId { get; set; }
    private Guid EditingParticipantIdentityUserId { get; set; }
    private string EditingParticipantResponseStatus { get; set; } = string.Empty;
    private bool EditingParticipantNotified { get; set; }
    private string EditingParticipantConcurrencyStamp { get; set; } = string.Empty;
    private ParticipantResponse ParticipantsResponseToAdd { get; set; } = ParticipantResponse.INVITED;

    // Project/Task lookup
    protected sealed class ProjectSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    protected sealed class ProjectTaskSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    private IReadOnlyList<ProjectSelectItem> ProjectsCollection { get; set; } = new List<ProjectSelectItem>();
    private IReadOnlyList<ProjectTaskSelectItem> ProjectTasksCollection { get; set; } = new List<ProjectTaskSelectItem>();

    private List<ProjectSelectItem> SelectedNewProject { get; set; } = new();
    private List<ProjectTaskSelectItem> SelectedNewProjectTask { get; set; } = new();
    private List<ProjectSelectItem> SelectedEditProject { get; set; } = new();
    private List<ProjectTaskSelectItem> SelectedEditProjectTask { get; set; } = new();

    // Read-only display for related entity in edit mode (replaces Select2)
    private Guid? RelatedEntityId { get; set; }
    private string? RelatedEntityDisplayName { get; set; }

    private Guid _loadedCalendarEventId;

    public CalendarEventDetail()
    {
        NewCalendarEvent = new CalendarEventCreateDto();
        EditingCalendarEvent = new CalendarEventUpdateDto();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetPermissionsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (CalendarEventId == Guid.Empty && CalendarEventIdQuery.HasValue)
        {
            CalendarEventId = CalendarEventIdQuery.Value;
        }

        if (CalendarEventId == Guid.Empty)
        {
            // Initialize create mode
            BreadcrumbItems.Clear();
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["CalendarEvents"], "/calendar-events"));
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["NewCalendarEvent"]));

            // Initialize new calendar-event
            NewCalendarEvent = new CalendarEventCreateDto
            {
                Title = string.Empty,
                StartTime = DateTime.Now,
                EndTime = DateTime.Now.AddHours(1),
                AllDay = false,
                EventType = EventType.MEETING.ToString(),
                RelatedType = RelatedType.NONE.ToString(),
                Visibility = EventVisibility.PRIVATE.ToString()
            };

            return;
        }

        if (_loadedCalendarEventId == CalendarEventId)
        {
            return;
        }

        _loadedCalendarEventId = CalendarEventId;

        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["CalendarEvents"], "/calendar-events"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Details"]));

        await LoadCalendarEventAsync();
        await LoadParticipantsAsync(page: 1);

        // Preload identity user lookup for better UX in members column.
        if (CanCreateCalendarEventParticipant && IdentityUsersCollection.Count == 0)
        {
            await GetIdentityUserCollectionLookupAsync();
        }
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateCalendarEventParticipant = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEventParticipants.Create);
        CanDeleteCalendarEventParticipant = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEventParticipants.Delete);
        CanEditCalendarEventParticipant = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEventParticipants.Edit);
        CanEditCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Edit);
        CanDeleteCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Delete);
        CanCreateCalendarEvent = await AuthorizationService.IsGrantedAsync(HCPermissions.CalendarEvents.Create);
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/calendar-events");
            return Task.CompletedTask;
        }, IconName.ArrowLeft);

        if (CalendarEventId == Guid.Empty && CanCreateCalendarEvent)
        {
            Toolbar.AddButton(L["Save"], CreateCalendarEventAsync, IconName.Save, Color.Primary);
        }
        else if (CalendarEventId != Guid.Empty && CanEditCalendarEvent)
        {
            Toolbar.AddButton(L["Save"], UpdateCalendarEventAsync, IconName.Save, Color.Primary);
        }

        if (CalendarEventId != Guid.Empty && CanDeleteCalendarEvent)
        {
            Toolbar.AddButton(L["Delete"], DeleteCalendarEventAsync, IconName.Delete, Color.Danger);
        }

        return ValueTask.CompletedTask;
    }

    private async Task LoadCalendarEventAsync()
    {
        IsLoadingCalendarEvent = true;
        try
        {
            CurrentCalendarEvent = await CalendarEventsAppService.GetAsync(CalendarEventId);

            // Initialize edit form
            if (CurrentCalendarEvent != null)
            {
                EditingCalendarEvent = ObjectMapper.Map<CalendarEventDto, CalendarEventUpdateDto>(CurrentCalendarEvent);
                // RelatedName and RelatedEntityId are populated by CalendarEventsAppService.GetAsync
                if (!string.IsNullOrEmpty(CurrentCalendarEvent.RelatedId))
                {
                    RelatedEntityId = CurrentCalendarEvent.RelatedEntityId;
                    RelatedEntityDisplayName = CurrentCalendarEvent.RelatedName;
                    if (CurrentCalendarEvent.RelatedType == RelatedType.PROJECT.ToString() && !string.IsNullOrWhiteSpace(RelatedEntityDisplayName))
                    {
                        SelectedEditProject = new List<ProjectSelectItem>
                        {
                            new() { Id = CurrentCalendarEvent.RelatedId, DisplayName = RelatedEntityDisplayName }
                        };
                    }
                    else if (CurrentCalendarEvent.RelatedType == RelatedType.TASK.ToString() && !string.IsNullOrWhiteSpace(RelatedEntityDisplayName))
                    {
                        SelectedEditProjectTask = new List<ProjectTaskSelectItem>
                        {
                            new() { Id = CurrentCalendarEvent.RelatedId, DisplayName = RelatedEntityDisplayName }
                        };
                    }
                }
                else
                {
                    RelatedEntityId = null;
                    RelatedEntityDisplayName = null;
                }
            }
        }
        finally
        {
            IsLoadingCalendarEvent = false;
        }
    }

    // ---------------------------
    // Participants
    // ---------------------------
    private async Task OnParticipantsGridReadAsync(DataGridReadDataEventArgs<CalendarEventParticipantWithNavigationPropertiesDto> e)
    {
        ParticipantsSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default)
            .Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : ""))
            .JoinAsString(",");

        ParticipantsCurrentPage = e.Page;
        await LoadParticipantsAsync(page: ParticipantsCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadParticipantsAsync(int page)
    {
        var input = new GetCalendarEventParticipantsInput
        {
            FilterText = ParticipantsFilterText,
            CalendarEventId = CalendarEventId,
            MaxResultCount = PageSize,
            SkipCount = (page - 1) * PageSize,
            Sorting = ParticipantsSorting
        };

        var result = await CalendarEventParticipantsAppService.GetListAsync(input);
        ParticipantsList = result.Items;
        ParticipantsTotalCount = (int)result.TotalCount;
        ParticipantsCurrentPage = page;
    }

    private async Task SearchParticipantsAsync()
    {
        ParticipantsCurrentPage = 1;
        await LoadParticipantsAsync(page: ParticipantsCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await CalendarEventParticipantsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return IdentityUsersCollection.ToList();
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? filter = null)
    {
        IdentityUsersCollection = (await CalendarEventParticipantsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
    }

    protected virtual void OnParticipantsToAddChanged()
    {
        if (IsParticipantResponseEditMode)
        {
            return;
        }

        // Select2 (single-select) may mutate the list in-place; force re-render so the Add button enables.
        InvokeAsync(StateHasChanged);
    }

    private async Task AddOrUpdateParticipantAsync()
    {
        if (CalendarEventId == Guid.Empty)
        {
            return;
        }

        if (IsParticipantResponseEditMode)
        {
            await UpdateParticipantResponseAsync();
            return;
        }

        if (!CanCreateCalendarEventParticipant)
        {
            return;
        }

        if (ParticipantsToAdd is null || ParticipantsToAdd.Count == 0)
        {
            return;
        }

        foreach (var user in ParticipantsToAdd)
        {
            try
            {
                // Avoid duplicate adds with a cheap existence check
                var exists = await CalendarEventParticipantsAppService.GetListAsync(new GetCalendarEventParticipantsInput
                {
                    CalendarEventId = CalendarEventId,
                    IdentityUserId = user.Id,
                    MaxResultCount = 1
                });

                if (exists.TotalCount > 0)
                {
                    continue;
                }

                await CalendarEventParticipantsAppService.CreateAsync(new CalendarEventParticipantCreateDto
                {
                    CalendarEventId = CalendarEventId,
                    IdentityUserId = user.Id,
                    ResponseStatus = ParticipantsResponseToAdd.ToString(),
                    Notified = false
                });
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex);
            }
        }

        ParticipantsToAdd = new List<LookupDto<Guid>>();
        await LoadParticipantsAsync(page: ParticipantsCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private void CancelParticipantResponseEdit()
    {
        IsParticipantResponseEditMode = false;
        EditingParticipantId = Guid.Empty;
        EditingParticipantIdentityUserId = Guid.Empty;
        EditingParticipantResponseStatus = string.Empty;
        EditingParticipantNotified = false;
        EditingParticipantConcurrencyStamp = string.Empty;

        ParticipantsToAdd = new List<LookupDto<Guid>>();

        InvokeAsync(StateHasChanged);
    }

    private async Task ToggleEditParticipantResponseAsync(CalendarEventParticipantWithNavigationPropertiesDto row)
    {
        if (!CanEditCalendarEventParticipant)
        {
            return;
        }

        if (IsParticipantResponseEditMode && EditingParticipantId == row.CalendarEventParticipant.Id)
        {
            CancelParticipantResponseEdit();
            return;
        }

        // Enter edit mode: fill user + response status, disable user select
        IsParticipantResponseEditMode = true;
        EditingParticipantId = row.CalendarEventParticipant.Id;
        EditingParticipantIdentityUserId = row.CalendarEventParticipant.IdentityUserId;
        EditingParticipantResponseStatus = row.CalendarEventParticipant.ResponseStatus;
        EditingParticipantNotified = row.CalendarEventParticipant.Notified;
        EditingParticipantConcurrencyStamp = row.CalendarEventParticipant.ConcurrencyStamp ?? string.Empty;

        // Parse ResponseStatus to enum
        if (Enum.TryParse<ParticipantResponse>(row.CalendarEventParticipant.ResponseStatus, out var responseStatus))
        {
            ParticipantsResponseToAdd = responseStatus;
        }

        // Fill select2 value (single-select uses a list)
        var displayName = row.IdentityUser?.UserName ?? row.IdentityUser?.Name ?? string.Empty;
        ParticipantsToAdd = new List<LookupDto<Guid>> { new() { Id = row.CalendarEventParticipant.IdentityUserId, DisplayName = displayName } };

        // Ensure selected user exists in datasource so Select2 can render it
        if (!IdentityUsersCollection.Any(x => x.Id == row.CalendarEventParticipant.IdentityUserId))
        {
            IdentityUsersCollection = IdentityUsersCollection.Concat(ParticipantsToAdd).ToList();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateParticipantResponseAsync()
    {
        if (!CanEditCalendarEventParticipant || !IsParticipantResponseEditMode || EditingParticipantId == Guid.Empty)
        {
            return;
        }

        await CalendarEventParticipantsAppService.UpdateAsync(EditingParticipantId, new CalendarEventParticipantUpdateDto
        {
            CalendarEventId = CalendarEventId,
            IdentityUserId = EditingParticipantIdentityUserId,
            ResponseStatus = ParticipantsResponseToAdd.ToString(),
            Notified = EditingParticipantNotified,
            ConcurrencyStamp = EditingParticipantConcurrencyStamp
        });

        await LoadParticipantsAsync(page: ParticipantsCurrentPage);
        CancelParticipantResponseEdit();
    }

    private async Task DeleteParticipantAsync(CalendarEventParticipantWithNavigationPropertiesDto input)
    {
        if (!CanDeleteCalendarEventParticipant)
        {
            return;
        }

        if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            return;
        }

        await CalendarEventParticipantsAppService.DeleteAsync(input.CalendarEventParticipant.Id);
        await LoadParticipantsAsync(page: ParticipantsCurrentPage);
        await LoadCalendarEventAsync();
        await InvokeAsync(StateHasChanged);
    }

    // -------------------------------
    // Create/Edit CalendarEvent Methods
    // -------------------------------

    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);

    // Manual validation methods
    private bool ValidateCreateCalendarEvent()
    {
        // Reset error state
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

        // Validate StartTime < EndTime
        if (NewCalendarEvent != null && NewCalendarEvent.StartTime >= NewCalendarEvent.EndTime)
        {
            CreateFieldErrors["EndTime"] = L["EndTimeMustBeAfterStartTime"];
            if (isValid)
            {
                CreateCalendarEventValidationErrorKey = "EndTimeMustBeAfterStartTime";
            }
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateEditCalendarEvent()
    {
        // Reset error state
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

        // Validate StartTime < EndTime
        if (EditingCalendarEvent != null && EditingCalendarEvent.StartTime >= EditingCalendarEvent.EndTime)
        {
            EditFieldErrors["EndTime"] = L["EndTimeMustBeAfterStartTime"];
            if (isValid)
            {
                EditCalendarEventValidationErrorKey = "EndTimeMustBeAfterStartTime";
            }
            isValid = false;
        }

         if (EditingCalendarEvent != null && EditingCalendarEvent.RelatedType == RelatedType.PROJECT.ToString())
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
        else if (EditingCalendarEvent != null && EditingCalendarEvent.RelatedType == RelatedType.TASK.ToString())
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

        return isValid;
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
            
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Set RelatedId based on RelatedType
            if (NewCalendarEvent != null && NewCalendarEvent.RelatedType == RelatedType.PROJECT.ToString())
            {
                NewCalendarEvent.RelatedId = SelectedNewProject.FirstOrDefault()?.Id;
            }
            else if (NewCalendarEvent != null && NewCalendarEvent.RelatedType == RelatedType.TASK.ToString())
            {
                NewCalendarEvent.RelatedId = SelectedNewProjectTask.FirstOrDefault()?.Id;
            }
            else
            {
                NewCalendarEvent!.RelatedId = null;
            }

            var createdCalendarEvent = await CalendarEventsAppService.CreateAsync(NewCalendarEvent);
            await UiMessageService.Success(L["SuccessfullyCreated"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));

            // Navigate to the created calendar-event detail
            NavigationManager.NavigateTo($"/calendar-event-detail/{createdCalendarEvent.Id}");
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
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            
            // Set RelatedId based on RelatedType
            if (EditingCalendarEvent != null && EditingCalendarEvent.RelatedType == RelatedType.PROJECT.ToString())
            {
                EditingCalendarEvent.RelatedId = SelectedEditProject.FirstOrDefault()?.Id;
            }
            else if (EditingCalendarEvent != null && EditingCalendarEvent.RelatedType == RelatedType.TASK.ToString())
            {
                EditingCalendarEvent.RelatedId = SelectedEditProjectTask.FirstOrDefault()?.Id;
            }
            else
            {
                EditingCalendarEvent!.RelatedId = null;
            }

            await CalendarEventsAppService.UpdateAsync(CalendarEventId, EditingCalendarEvent);
            await UiMessageService.Success(L["SuccessfullyUpdated"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));

            // Reload calendar-event data
            await LoadCalendarEventAsync();
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


    private async Task DeleteCalendarEventAsync()
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value,
            options: new Action<UiMessageOptions>(options => options.ConfirmButtonText = L["Confirm"])))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            await CalendarEventsAppService.DeleteAsync(CalendarEventId);
            await UiMessageService.Success(L["SuccessfullyDeleted"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            NavigationManager.NavigateTo("/calendar-events");
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
       
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

    private ParticipantResponse ParseResponseStatus(string responseStatus)
    {
        return responseStatus switch
        {
            "INVITED" => ParticipantResponse.INVITED,
            "ACCEPTED" => ParticipantResponse.ACCEPTED,
            "DECLINED" => ParticipantResponse.DECLINED,
            _ => ParticipantResponse.INVITED,
        };
    }
}

