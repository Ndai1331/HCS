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
using HC.Blazor.Shared;
using HC.Blazor.Components.ProjectTaskCreateModal;
using Volo.Abp.AspNetCore.Components.Messages;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages;

public partial class ProjectDetail : HCComponentBase
{
    // Accept route param and query param (?id=...).
    [Parameter] public Guid ProjectId { get; set; }

    [SupplyParameterFromQuery(Name = "id")]
    public Guid? ProjectIdQuery { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; set; } = new();

    protected PageToolbar Toolbar { get; } = new PageToolbar();

    protected string PageTitle
    {
        get
        {
            if (ProjectId == Guid.Empty)
                return L["NewProject"];
            return CurrentProject?.Project is null
                ? L["Projects"]
                : $"{CurrentProject.Project.Code} - {CurrentProject.Project.Name}";
        }
    }

    protected bool IsLoadingProject { get; set; }
    protected ProjectWithNavigationPropertiesDto? CurrentProject { get; set; }

    // Create/Edit Project properties
    private ProjectCreateDto NewProject { get; set; }
    private ProjectUpdateDto EditingProject { get; set; }

    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Validation error keys
    private string? CreateProjectValidationErrorKey { get; set; }
    private string? EditProjectValidationErrorKey { get; set; }

    // Department collections
    private IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedDepartment { get; set; } = new();
    private List<LookupDto<Guid>> SelectedEditDepartment { get; set; } = new();

    // Date pickers
    private DatePicker<DateTime>? NewProjectStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewProjectEndDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditingProjectStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditingProjectEndDateDatePicker { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;

    // Tasks tab
    public DataGrid<ProjectTaskWithNavigationPropertiesDto>? TasksDataGridRef { get; set; }
    private IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> TasksList { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();
    private int TasksTotalCount { get; set; }
    private int TasksCurrentPage { get; set; } = 1;
    private string TasksSorting { get; set; } = string.Empty;
    private string? TasksFilterText { get; set; }
    private List<ProjectTaskWithNavigationPropertiesDto> SelectedTasks { get; set; } = new();
    private ProjectTaskWithNavigationPropertiesDto? SelectedTask { get; set; }

    // Child tasks dictionary for all parent tasks (loaded at initialization)
    // Key: parent task Code (not ID), Value: list of child tasks
    private Dictionary<string, List<ProjectTaskWithNavigationPropertiesDto>> ChildTasksByParentCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Set of expanded task codes for the tree view (auto-expanded by default)
    private HashSet<string> ExpandedTasks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

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


    private bool CanCreateProject { get; set; }
    private bool CanEditProject { get; set; }
    private bool CanDeleteProject { get; set; }
    private bool CanCreateProjectTask { get; set; }



    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> MembersToAdd { get; set; } = new();
    private ProjectMemberRole MembersRoleToAdd { get; set; } = ProjectMemberRole.MEMBER;
    private bool IsMemberRoleEditMode { get; set; }
    private Guid EditingMemberId { get; set; }
    private Guid EditingMemberUserId { get; set; }
    private DateTime EditingMemberJoinedAt { get; set; }
    private string EditingMemberConcurrencyStamp { get; set; } = string.Empty;

    private Guid _loadedProjectId;

    // Project Task Create Modal
    private ProjectTaskCreateModal? ProjectTaskCreateModalRef { get; set; }

    public ProjectDetail()
    {
        NewProject = new ProjectCreateDto();
        EditingProject = new ProjectUpdateDto();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await OnSetupProjectAsync();
            await SetPermissionsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task OnSetupProjectAsync()
    {
        if (ProjectId == Guid.Empty && ProjectIdQuery.HasValue)
        {
            ProjectId = ProjectIdQuery.Value;
        }

        if (ProjectId == Guid.Empty)
        {
            // Initialize create mode
            BreadcrumbItems.Clear();
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Projects"], "/projects"));
            BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["NewProject"]));

            // Initialize new project
            NewProject = new ProjectCreateDto
            {
                StartDate = DateTime.Now,
                EndDate = DateTime.Now,
                Code = await GenerateNextProjectCodeAsync(),
                Status = ProjectStatus.PLANNING
            };
            SelectedDepartment = new List<LookupDto<Guid>>();
            await GetDepartmentCollectionLookupAsync();
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
        CanEditProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Edit);
        CanDeleteProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Delete);
        CanCreateProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Create);
        CanCreateProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Create);
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["Back"], () =>
        {
            NavigationManager.NavigateTo("/projects");
            return Task.CompletedTask;
        }, IconName.ArrowLeft);

        if (ProjectId == Guid.Empty && CanCreateProject)
        {
            Toolbar.AddButton(L["Save"], CreateProjectAsync, IconName.Save, Color.Primary);
        }
        else if (CurrentProject != null && CanEditProject)
        {
            Toolbar.AddButton(L["Save"], UpdateProjectAsync, IconName.Save, Color.Primary);
        }

        if (CurrentProject != null && CanDeleteProject)
        {
            Toolbar.AddButton(L["Delete"], DeleteProjectAsync, IconName.Delete, Color.Danger);
        }

        return ValueTask.CompletedTask;
    }

    private async Task LoadProjectAsync()
    {
        IsLoadingProject = true;
        try
        {
            CurrentProject = await ProjectsAppService.GetWithNavigationPropertiesAsync(ProjectId);

            // Initialize edit form
            if (CurrentProject != null)
            {
                var mappedProject = ObjectMapper.Map<ProjectDto, ProjectUpdateDto>(CurrentProject.Project);
                EditingProject = new ProjectUpdateDto
                {
                    Code = mappedProject?.Code ?? CurrentProject.Project.Code ?? string.Empty,
                    Name = mappedProject?.Name ?? CurrentProject.Project.Name ?? string.Empty,
                    Description = mappedProject?.Description ?? CurrentProject.Project.Description,
                    StartDate = mappedProject?.StartDate ?? CurrentProject.Project.StartDate,
                    EndDate = mappedProject?.EndDate ?? CurrentProject.Project.EndDate,
                    Status = mappedProject?.Status ?? CurrentProject.Project.Status,
                    OwnerDepartmentId = mappedProject?.OwnerDepartmentId ?? CurrentProject.Project.OwnerDepartmentId,
                    ConcurrencyStamp = mappedProject?.ConcurrencyStamp ?? CurrentProject.Project.ConcurrencyStamp ?? string.Empty
                };

                await GetDepartmentCollectionLookupAsync();
                // Set selected department for Select2
                if (EditingProject.OwnerDepartmentId.HasValue && DepartmentsCollection != null)
                {
                    var selectedDept = DepartmentsCollection.FirstOrDefault(d => d.Id == EditingProject.OwnerDepartmentId.Value);
                    SelectedEditDepartment = selectedDept != null ? new List<LookupDto<Guid>> { selectedDept } : new List<LookupDto<Guid>>();
                }
                else
                {
                    SelectedEditDepartment = new List<LookupDto<Guid>>();
                }
            }
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
        // Load parent tasks
        var input = new GetProjectTasksInput
        {
            ProjectId = ProjectId,
            OnlyParentTasks = true, // Only load tasks without ParentTaskId (parent tasks)
            FilterText = TasksFilterText,
            ParentTaskId = null,
            MaxResultCount = PageSize,
            SkipCount = (page - 1) * PageSize,
            Sorting = TasksSorting
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        TasksList = result.Items;
        TasksTotalCount = (int)result.TotalCount;
        TasksCurrentPage = page;

        // Debug: Log each child count
        foreach (var task in result.Items)
        {
            Logger.LogInformation("Parent task loaded: {TaskId} - {ChildTaskCount}", task.ProjectTask.Id, task.ChildTaskCount);
        }

        // Load all child tasks for the project (only on first page load)
        await LoadAllChildTasksAsync();

        // Auto-expand all tasks that have children (including child of child, grandchild, etc.)
        ExpandedTasks.Clear();
        
        // First, expand all root parent tasks that have children
        foreach (var task in result.Items)
        {
            if (task.ChildTaskCount > 0)
            {
                ExpandedTasks.Add(task.ProjectTask.Code);
                Logger.LogInformation("Auto-expanding parent task: {TaskCode}", task.ProjectTask.Code);
            }
        }
        
        // Then, recursively expand all child tasks that have children
        foreach (var task in result.Items)
        {
            ExpandAllChildrenRecursively(task.ProjectTask.Code);
        }
        
        Logger.LogInformation("Auto-expanded {Count} tasks total", ExpandedTasks.Count);
    }

    private async Task LoadAllChildTasksAsync()
    {
        Logger.LogInformation("Loading all child tasks for project: {ProjectId}", ProjectId);

        // Load only child tasks using pagination (to avoid validation limits)
        var allChildTasks = new List<ProjectTaskWithNavigationPropertiesDto>();
        int skipCount = 0;
        const int batchSize = 200; // Use reasonable batch size
        bool hasMore = true;

        while (hasMore)
        {
            var input = new GetProjectTasksInput
            {
                ProjectId = ProjectId,
                OnlyChildTasks = true, // Only load tasks with ParentTaskId (child tasks)
                FilterText = null,
                MaxResultCount = batchSize,
                SkipCount = skipCount,
                Sorting = "ProjectTask.Title ASC"
            };

            var result = await ProjectTasksAppService.GetListAsync(input);

            // Debug: Log each child count
            foreach (var task in result.Items)
            {
                Logger.LogInformation("Child task loaded: {TaskId} - {ParentTaskId}", task.ProjectTask.Id, task.ProjectTask.ParentTaskId);
            }
            
            if (result.Items.Count == 0)
            {
                hasMore = false;
            }
            else
            {
                allChildTasks.AddRange(result.Items);
                skipCount += result.Items.Count;
                
                // Check if we've loaded all items
                if (allChildTasks.Count >= result.TotalCount)
                {
                    hasMore = false;
                }
            }
        }
        
        // Group child tasks by parent task Code (ParentTaskId stores parent task's Code as string)
        ChildTasksByParentCode = allChildTasks
            .GroupBy(t => t.ProjectTask.ParentTaskId!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        Logger.LogInformation("Loaded {Count} child tasks for {ParentCount} parent tasks", 
            ChildTasksByParentCode.Values.Sum(v => v.Count), ChildTasksByParentCode.Count);
        
        // Debug: Log parent codes with child tasks
        foreach (var kvp in ChildTasksByParentCode)
        {
            Logger.LogInformation("ParentTaskCode: {ParentCode}, ChildTaskCount: {Count}", kvp.Key, kvp.Value.Count);
        }
    }

    // Helper method to get child tasks for a parent task (by parent's Code)
    private List<ProjectTaskWithNavigationPropertiesDto> GetChildTasksForParent(string parentCode)
    {
        var result = ChildTasksByParentCode.ContainsKey(parentCode) ? ChildTasksByParentCode[parentCode] : new List<ProjectTaskWithNavigationPropertiesDto>();
        Logger.LogInformation("GetChildTasksForParent called for ParentCode: {ParentCode}, Found: {Count} child tasks", parentCode, result.Count);
        return result;
    }

    /// <summary>
    /// Recursively expand all tasks that have children
    /// </summary>
    private void ExpandAllChildrenRecursively(string parentCode)
    {
        var childTasks = GetChildTasksForParent(parentCode);
        
        foreach (var childTask in childTasks)
        {
            // If this child has its own children, expand it and continue recursively
            if (childTask.ChildTaskCount > 0)
            {
                ExpandedTasks.Add(childTask.ProjectTask.Code);
                Logger.LogInformation("Auto-expanding child task: {TaskCode}", childTask.ProjectTask.Code);
                
                // Recursively expand this child's children
                ExpandAllChildrenRecursively(childTask.ProjectTask.Code);
            }
        }
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

    protected bool RowSelectableHandler(RowSelectableEventArgs<ProjectTaskWithNavigationPropertiesDto> rowSelectableEventArgs)
    {
        return rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick;
    }

    protected Task SelectedTaskRowsChanged()
    {
        return Task.CompletedTask;
    }

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
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
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
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
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

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
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
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
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

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await ProjectMembersAppService.DeleteAsync(input.ProjectMember.Id);
            await LoadMembersAsync(page: MembersCurrentPage);
            await LoadProjectAsync();
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

    // -------------------------------
    // Create/Edit Project Methods
    // -------------------------------

    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);

    // Manual validation methods
    private bool ValidateCreateProject()
    {
        // Reset error state
        CreateProjectValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: Code
        if (string.IsNullOrWhiteSpace(NewProject?.Code))
        {
            CreateFieldErrors["Code"] = L["CodeRequired"];
            CreateProjectValidationErrorKey = "CodeRequired";
            isValid = false;
        }

        // Required: Name
        if (string.IsNullOrWhiteSpace(NewProject?.Name))
        {
            CreateFieldErrors["Name"] = L["NameRequired"];
            if (isValid)
            {
                CreateProjectValidationErrorKey = "NameRequired";
            }
            isValid = false;
        }

        // EndDate must not be before StartDate (allow same day)
        if (NewProject != null && NewProject.EndDate < NewProject.StartDate)
        {
            CreateFieldErrors["EndDate"] = L["EndDateMustNotBeBeforeStartDate"];
            if (isValid)
            {
                CreateProjectValidationErrorKey = "EndDateMustNotBeBeforeStartDate";
            }
            isValid = false;
        }

        return isValid;
    }

    private bool ValidateEditProject()
    {
        // Reset error state
        EditProjectValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        // Required: Code
        if (string.IsNullOrWhiteSpace(EditingProject?.Code))
        {
            EditFieldErrors["Code"] = L["CodeRequired"];
            EditProjectValidationErrorKey = "CodeRequired";
            isValid = false;
        }

        // Required: Name
        if (string.IsNullOrWhiteSpace(EditingProject?.Name))
        {
            EditFieldErrors["Name"] = L["NameRequired"];
            if (isValid)
            {
                EditProjectValidationErrorKey = "NameRequired";
            }
            isValid = false;
        }

        // EndDate must not be before StartDate (allow same day)
        if (EditingProject != null && EditingProject.EndDate < EditingProject.StartDate)
        {
            EditFieldErrors["EndDate"] = L["EndDateMustNotBeBeforeStartDate"];
            if (isValid)
            {
                EditProjectValidationErrorKey = "EndDateMustNotBeBeforeStartDate";
            }
            isValid = false;
        }

        return isValid;
    }

    private async Task CreateProjectAsync()
    {
        try
        {
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

            // Navigate to the created project detail
            NavigationManager.NavigateTo($"/project-detail/{createdProject.Id}", true);
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

    private async Task UpdateProjectAsync()
    {
        try
        {
            if (!ValidateEditProject())
            {
                await UiMessageService.Warn(L[EditProjectValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await ProjectsAppService.UpdateAsync(ProjectId, EditingProject);
            await UiMessageService.Success(L["SuccessfullyUpdated"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));

            // Reload project data
            await LoadProjectAsync();
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

    // Generate next available Project code (Pxxxxxxx format)
    private async Task<string> GenerateNextProjectCodeAsync()
    {
        try
        {
            int maxNumber = 0;
            const int pageSize = 200;
            int skipCount = 0;
            bool hasMore = true;
            var foundCodes = new List<string>();

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
                                foundCodes.Add(code);

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

            var nextCode = $"P{(maxNumber + 1):D7}";
            return nextCode;
        }
        catch (Exception)
        {
            // Fallback to P0000001 if error occurs
            return "P0000001";
        }
    }


    private async Task DeleteProjectAsync()
    {
        try
        {
            if (!await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            await ProjectsAppService.DeleteAsync(ProjectId);
            await UiMessageService.Success(L["SuccessfullyDeleted"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            NavigationManager.NavigateTo("/projects");
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

    private async Task GetDepartmentCollectionLookupAsync(string? newValue = null)
    {
        DepartmentsCollection = (await ProjectsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task<List<LookupDto<Guid>>> GetDepartmentCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        DepartmentsCollection = (await ProjectsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return DepartmentsCollection.ToList();
    }

    protected virtual void OnDepartmentIdChanged()
    {
        NewProject.OwnerDepartmentId = SelectedDepartment?.FirstOrDefault()?.Id;
    }

    protected virtual void OnEditDepartmentIdChanged()
    {
        EditingProject.OwnerDepartmentId = SelectedEditDepartment?.FirstOrDefault()?.Id;
    }

    // ---------------------------
    // Create Task from Project Detail
    // ---------------------------
    private async Task OpenCreateTaskModalAsync()
    {
        if (ProjectId == Guid.Empty)
        {
            return;
        }

        if (ProjectTaskCreateModalRef != null)
        {
            await ProjectTaskCreateModalRef.OpenCreateProjectTaskModalAsync();
        }
    }

    private async Task OnTaskCreatedAsync()
    {
        // Refresh tasks grid after a new task is created
        TasksCurrentPage = 1; // Reset to first page to see new tasks
        await LoadTasksAsync(page: 1);
        await InvokeAsync(StateHasChanged);
    }

    // ---------------------------
    // TaskTree Helper Methods
    // ---------------------------

    /// <summary>
    /// Get child task count for a task
    /// </summary>
    private int GetChildTaskCount(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ChildTaskCount;
    }

    /// <summary>
    /// Get child tasks for a task
    /// </summary>
    private List<ProjectTaskWithNavigationPropertiesDto> GetChildTasksForTask(ProjectTaskWithNavigationPropertiesDto task)
    {
        return GetChildTasksForParent(task.ProjectTask.Code);
    }

    /// <summary>
    /// Get task code
    /// </summary>
    private string GetTaskCode(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.Code;
    }

    /// <summary>
    /// Get task title
    /// </summary>
    private string GetTaskTitle(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.Title;
    }

    /// <summary>
    /// Get task start date
    /// </summary>
    private DateTime GetTaskStartDate(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.StartDate;
    }

    /// <summary>
    /// Get task due date
    /// </summary>
    private DateTime GetTaskDueDate(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.DueDate;
    }

    /// <summary>
    /// Get task status
    /// </summary>
    private ProjectTaskStatus GetTaskStatus(ProjectTaskWithNavigationPropertiesDto task)
    {
        return ParseStatus(task.ProjectTask.Status);
    }

    /// <summary>
    /// Get task priority
    /// </summary>
    private ProjectTaskPriority GetTaskPriority(ProjectTaskWithNavigationPropertiesDto task)
    {
        return ParsePriority(task.ProjectTask.Priority);
    }

    /// <summary>
    /// Get task progress percent
    /// </summary>
    private int GetTaskProgressPercent(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.ProgressPercent;
    }

    /// <summary>
    /// Get task detail URL
    /// </summary>
    private string GetTaskDetailUrl(ProjectTaskWithNavigationPropertiesDto task)
    {
        return $"/project-task-detail/{task.ProjectTask.Id}";
    }

    /// <summary>
    /// Get parent task ID
    /// </summary>
    private string GetTaskParentTaskId(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ProjectTask.ParentTaskId ?? string.Empty;
    }

    /// <summary>
    /// Get parent task title
    /// </summary>
    private string GetParentTaskTitle(ProjectTaskWithNavigationPropertiesDto task)
    {
        return task.ParentTaskTitle ?? string.Empty;
    }

    /// <summary>
    /// Get status badge color
    /// </summary>
    private Color GetStatusBadgeColor(ProjectTaskStatus status)
    {
        return EnumStatusColorHelper.GetProjectTaskStatusBadgeColor(status);
    }

    /// <summary>
    /// Get priority badge color
    /// </summary>
    private Color GetPriorityBadgeColor(ProjectTaskPriority priority)
    {
        return EnumStatusColorHelper.GetProjectTaskPriorityBadgeColor(priority);
    }

    /// <summary>
    /// Handle task expanded event from TaskTree
    /// </summary>
    private async Task HandleTaskExpanded((string TaskCode, bool IsExpanded) eventArgs)
    {
        // Update the expanded state in the parent's HashSet
        if (eventArgs.IsExpanded)
        {
            ExpandedTasks.Add(eventArgs.TaskCode);
        }
        else
        {
            ExpandedTasks.Remove(eventArgs.TaskCode);
        }

        Logger.LogInformation("Task {TaskCode} expanded: {IsExpanded}, Total expanded: {Count}",
            eventArgs.TaskCode, eventArgs.IsExpanded, ExpandedTasks.Count);

        await InvokeAsync(StateHasChanged);
    }
}

