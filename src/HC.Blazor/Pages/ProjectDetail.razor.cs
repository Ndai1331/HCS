using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using HC.Permissions;
using HC.ProjectMembers;
using HC.ProjectTasks;
using HC.Projects;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;

namespace HC.Blazor.Pages;

public partial class ProjectDetail : HCComponentBase
{
    // Accept route param and query param (?id=...).
    [Parameter] public Guid ProjectId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? ProjectIdQuery { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();

    protected PageToolbar Toolbar { get; } = new PageToolbar();

    protected string PageTitle => CurrentProject?.Project is null
        ? L["Projects"]
        : $"{CurrentProject.Project.Code} - {CurrentProject.Project.Name}";

    protected bool IsLoadingProject { get; set; }
    protected ProjectWithNavigationPropertiesDto? CurrentProject { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;

    // Tasks tab
    public DataGrid<ProjectTaskWithNavigationPropertiesDto>? TasksDataGridRef { get; set; }
    private IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> TasksList { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();
    private int TasksTotalCount { get; set; }
    private int TasksCurrentPage { get; set; } = 1;
    private string TasksSorting { get; set; } = string.Empty;
    private string? TasksFilterText { get; set; }

    // Members tab
    public DataGrid<ProjectMemberWithNavigationPropertiesDto>? MembersDataGridRef { get; set; }
    private IReadOnlyList<ProjectMemberWithNavigationPropertiesDto> MembersList { get; set; } = new List<ProjectMemberWithNavigationPropertiesDto>();
    private int MembersTotalCount { get; set; }
    private int MembersCurrentPage { get; set; } = 1;
    private string MembersSorting { get; set; } = string.Empty;
    private string? MembersFilterText { get; set; }

    // Member add/edit role UI
    private bool CanCreateProjectMember { get; set; }
    private bool CanDeleteProjectMember { get; set; }
    private bool CanEditProjectMember { get; set; }
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> MembersToAdd { get; set; } = new();
    private ProjectMemberRole MembersRoleToAdd { get; set; } = ProjectMemberRole.MEMBER;
    private bool IsMemberRoleEditMode { get; set; }
    private Guid EditingMemberId { get; set; }
    private Guid EditingMemberUserId { get; set; }
    private DateTime EditingMemberJoinedAt { get; set; }
    private string EditingMemberConcurrencyStamp { get; set; } = string.Empty;

    private Guid _loadedProjectId;

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected override async Task OnParametersSetAsync()
    {
        if (ProjectId == Guid.Empty && ProjectIdQuery.HasValue)
        {
            ProjectId = ProjectIdQuery.Value;
        }

        if (ProjectId == Guid.Empty)
        {
            return;
        }

        if (_loadedProjectId == ProjectId)
        {
            return;
        }

        _loadedProjectId = ProjectId;

        BreadcrumbItems.Clear();
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Projects"], "/projects"));
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Details"]));

        await LoadProjectAsync();
        await LoadTasksAsync(page: 1);
        await LoadMembersAsync(page: 1);

        // Preload identity user lookup for better UX in members column.
        if (CanCreateProjectMember && IdentityUsersCollection.Count == 0)
        {
            await GetIdentityUserCollectionLookupAsync();
        }
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Create);
        CanDeleteProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Delete);
        CanEditProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Edit);
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/projects");
            return Task.CompletedTask;
        }, IconName.ArrowLeft);
        return ValueTask.CompletedTask;
    }

    private async Task LoadProjectAsync()
    {
        IsLoadingProject = true;
        try
        {
            CurrentProject = await ProjectsAppService.GetWithNavigationPropertiesAsync(ProjectId);
        }
        finally
        {
            IsLoadingProject = false;
        }
    }

    // ---------------------------
    // Tasks
    // ---------------------------
    private async Task OnTasksGridReadAsync(DataGridReadDataEventArgs<ProjectTaskWithNavigationPropertiesDto> e)
    {
        TasksSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default)
            .Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : ""))
            .JoinAsString(",");

        TasksCurrentPage = e.Page;
        await LoadTasksAsync(page: TasksCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadTasksAsync(int page)
    {
        var input = new GetProjectTasksInput
        {
            ProjectId = ProjectId,
            FilterText = TasksFilterText,
            MaxResultCount = PageSize,
            SkipCount = (page - 1) * PageSize,
            Sorting = TasksSorting
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        TasksList = result.Items;
        TasksTotalCount = (int)result.TotalCount;
        TasksCurrentPage = page;
    }

    private async Task SearchTasksAsync()
    {
        TasksCurrentPage = 1;
        await LoadTasksAsync(page: TasksCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshTasksAsync()
    {
        await LoadTasksAsync(page: TasksCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    // Helpers: parse enums stored as string in DTOs.
    protected ProjectTaskStatus ParseStatus(string? status)
    {
        return Enum.TryParse<ProjectTaskStatus>(status ?? string.Empty, ignoreCase: true, out var parsed)
            ? parsed
            : ProjectTaskStatus.TODO;
    }

    protected ProjectTaskPriority ParsePriority(string? priority)
    {
        return Enum.TryParse<ProjectTaskPriority>(priority ?? string.Empty, ignoreCase: true, out var parsed)
            ? parsed
            : ProjectTaskPriority.LOW;
    }

    protected string GetStatusText(ProjectTaskStatus status) => L[$"Enum:ProjectTaskStatus.{status}"];
    protected string GetPriorityText(ProjectTaskPriority priority) => L[$"Enum:ProjectTaskPriority.{priority}"];


    // ---------------------------
    // Members
    // ---------------------------
    private async Task OnMembersGridReadAsync(DataGridReadDataEventArgs<ProjectMemberWithNavigationPropertiesDto> e)
    {
        MembersSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default)
            .Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : ""))
            .JoinAsString(",");

        MembersCurrentPage = e.Page;
        await LoadMembersAsync(page: MembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadMembersAsync(int page)
    {
        var input = new GetProjectMembersInput
        {
            FilterText = MembersFilterText,
            ProjectId = ProjectId,
            MaxResultCount = PageSize,
            SkipCount = (page - 1) * PageSize,
            Sorting = MembersSorting
        };

        var result = await ProjectMembersAppService.GetListAsync(input);
        MembersList = result.Items;
        MembersTotalCount = (int)result.TotalCount;
        MembersCurrentPage = page;
    }

    private async Task SearchMembersAsync()
    {
        MembersCurrentPage = 1;
        await LoadMembersAsync(page: MembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return IdentityUsersCollection.ToList();
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? filter = null)
    {
        IdentityUsersCollection = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
    }

    protected virtual void OnMembersToAddChanged()
    {
        if (IsMemberRoleEditMode)
        {
            return;
        }

        // Select2 (single-select) may mutate the list in-place; force re-render so the Add button enables.
        InvokeAsync(StateHasChanged);
    }

    private async Task AddOrUpdateMemberAsync()
    {
        if (ProjectId == Guid.Empty)
        {
            return;
        }

        if (IsMemberRoleEditMode)
        {
            await UpdateMemberRoleAsync();
            return;
        }

        if (!CanCreateProjectMember)
        {
            return;
        }

        if (MembersToAdd is null || MembersToAdd.Count == 0)
        {
            return;
        }

        foreach (var user in MembersToAdd)
        {
            try
            {
                // Avoid duplicate adds with a cheap existence check
                var exists = await ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
                {
                    ProjectId = ProjectId,
                    UserId = user.Id,
                    MaxResultCount = 1
                });

                if (exists.TotalCount > 0)
                {
                    continue;
                }

                await ProjectMembersAppService.CreateAsync(new ProjectMemberCreateDto
                {
                    ProjectId = ProjectId,
                    UserId = user.Id,
                    MemberRole = MembersRoleToAdd,
                    JoinedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex);
            }
        }

        MembersToAdd = new List<LookupDto<Guid>>();
        await LoadMembersAsync(page: MembersCurrentPage);
        await LoadProjectAsync();
        await InvokeAsync(StateHasChanged);
    }

    private void CancelMemberRoleEdit()
    {
        IsMemberRoleEditMode = false;
        EditingMemberId = Guid.Empty;
        EditingMemberUserId = Guid.Empty;
        EditingMemberConcurrencyStamp = string.Empty;

        MembersToAdd = new List<LookupDto<Guid>>();
        MembersRoleToAdd = ProjectMemberRole.MEMBER;

        InvokeAsync(StateHasChanged);
    }

    private async Task ToggleEditMemberRoleAsync(ProjectMemberWithNavigationPropertiesDto row)
    {
        if (!CanEditProjectMember)
        {
            return;
        }

        if (IsMemberRoleEditMode && EditingMemberId == row.ProjectMember.Id)
        {
            CancelMemberRoleEdit();
            return;
        }

        // Enter edit mode: fill user + role, disable user select
        IsMemberRoleEditMode = true;
        EditingMemberId = row.ProjectMember.Id;
        EditingMemberUserId = row.ProjectMember.UserId;
        EditingMemberJoinedAt = row.ProjectMember.JoinedAt;
        EditingMemberConcurrencyStamp = row.ProjectMember.ConcurrencyStamp ?? string.Empty;

        MembersRoleToAdd = row.ProjectMember.MemberRole;

        // Fill select2 value (single-select uses a list)
        var displayName = row.User?.UserName ?? row.User?.Name ?? string.Empty;
        MembersToAdd = new List<LookupDto<Guid>> { new() { Id = row.ProjectMember.UserId, DisplayName = displayName } };

        // Ensure selected user exists in datasource so Select2 can render it
        if (!IdentityUsersCollection.Any(x => x.Id == row.ProjectMember.UserId))
        {
            IdentityUsersCollection = IdentityUsersCollection.Concat(MembersToAdd).ToList();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateMemberRoleAsync()
    {
        if (!CanEditProjectMember || !IsMemberRoleEditMode || EditingMemberId == Guid.Empty)
        {
            return;
        }

        await ProjectMembersAppService.UpdateAsync(EditingMemberId, new ProjectMemberUpdateDto
        {
            ProjectId = ProjectId,
            UserId = EditingMemberUserId,
            MemberRole = MembersRoleToAdd,
            JoinedAt = EditingMemberJoinedAt,
            ConcurrencyStamp = EditingMemberConcurrencyStamp
        });

        await LoadMembersAsync(page: MembersCurrentPage);
        await LoadProjectAsync();
        CancelMemberRoleEdit();
    }

    private async Task DeleteMemberAsync(ProjectMemberWithNavigationPropertiesDto input)
    {
        if (!CanDeleteProjectMember)
        {
            return;
        }

        if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            return;
        }

        await ProjectMembersAppService.DeleteAsync(input.ProjectMember.Id);
        await LoadMembersAsync(page: MembersCurrentPage);
        await LoadProjectAsync();
        await InvokeAsync(StateHasChanged);
    }
}

