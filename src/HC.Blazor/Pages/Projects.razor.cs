using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Globalization;
using System.IO;
using System.Web;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Chat.Helpers;
using HC.Projects;
using HC.ProjectMembers;
using HC.ProjectTasks;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using HC.Blazor;
using HC.Blazor.Shared;
using Microsoft.Extensions.Logging;
using HC.Chat.Conversations;
using Volo.Abp.AspNetCore.Components.Messages;

namespace HC.Blazor.Pages;

public partial class Projects : HCComponentBase
{
    [Inject] private ILogger<Projects>? Logger { get; set; }
    
    [Inject]
    public IConversationAppService ConversationAppService { get; set; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<ProjectWithNavigationPropertiesDto>? DataGridRef { get; set; }

    private IReadOnlyList<ProjectWithNavigationPropertiesDto> ProjectList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateProject { get; set; }

    private bool CanEditProject { get; set; }

    private bool CanDeleteProject { get; set; }

    private bool CanCreateProjectMember { get; set; }
    private bool CanDeleteProjectMember { get; set; }
    private bool CanEditProjectMember { get; set; }

    private ProjectCreateDto NewProject { get; set; }

    private Validations NewProjectValidations { get; set; } = new();
    private ProjectUpdateDto EditingProject { get; set; }

    private Validations EditingProjectValidations { get; set; } = new();
    
    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();
    
    // Validation error keys
    private string? CreateProjectValidationErrorKey { get; set; }
    private string? EditProjectValidationErrorKey { get; set; }
    
    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);
    private Guid EditingProjectId { get; set; }

    private Modal CreateProjectModal { get; set; } = new();
    private Modal EditProjectModal { get; set; } = new();
    private GetProjectsInput Filter { get; set; }
    private string StatusFilterValue { get; set; } = string.Empty;
    private string OwnerDepartmentFilterValue { get; set; } = string.Empty;

    private DataGridEntityActionsColumn<ProjectWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "project-create-tab";
    protected string SelectedEditTab = "project-edit-tab";

    private IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedDepartment { get; set; } = new();
    private List<LookupDto<Guid>> SelectedEditDepartment { get; set; } = new();
    private List<ProjectWithNavigationPropertiesDto> SelectedProjects { get; set; } = new();
    private bool AllProjectsSelected { get; set; }

    // Project members management
    private Guid? CreatedProjectId { get; set; }
    private List<LookupDto<Guid>> SelectedProjectMembers { get; set; } = new();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();

    // Edit modal - lazy loading for members tab
    private Guid? EditingProjectMembersLoadedForProjectId { get; set; }
    private bool IsIdentityUsersLookupLoaded { get; set; }
    private bool IsProjectMembersTabVisited { get; set; }

    // Edit modal - force remount validations to reset state
    private int EditingProjectValidationsKey { get; set; }

    // Project Tasks modal
    private Modal ProjectTasksModal { get; set; } = new();
    public DataGrid<ProjectTaskWithNavigationPropertiesDto>? ProjectTasksDataGridRef { get; set; }
    private IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> ProjectTasksList { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();
    private int ProjectTasksTotalCount { get; set; }
    private Guid ProjectTasksProjectId { get; set; }
    private string ProjectTasksSorting { get; set; } = string.Empty;
    private int ProjectTasksCurrentPage { get; set; } = 1;
    private string? ProjectTasksFilterText { get; set; }
    private string? ProjectTasksProjectDisplayName { get; set; }
    private Dictionary<string, List<ProjectTaskWithNavigationPropertiesDto>> ProjectTaskChildTasksByParentCode { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> ExpandedProjectTasks { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Project Members modal
    private Modal ProjectMembersModal { get; set; } = new();
    public DataGrid<ProjectMemberWithNavigationPropertiesDto>? ProjectMembersDataGridRef { get; set; }
    private IReadOnlyList<ProjectMemberWithNavigationPropertiesDto> ProjectMembersList { get; set; } = new List<ProjectMemberWithNavigationPropertiesDto>();
    private int ProjectMembersTotalCount { get; set; }
    private Guid ProjectMembersProjectId { get; set; }
    private string ProjectMembersSorting { get; set; } = string.Empty;
    private int ProjectMembersCurrentPage { get; set; } = 1;
    private List<LookupDto<Guid>> ProjectMembersToAdd { get; set; } = new();
    private ProjectMemberRole ProjectMembersRoleToAdd { get; set; } = ProjectMemberRole.MEMBER;
    private string? ProjectMembersFilterText { get; set; }
    private string? ProjectMembersProjectDisplayName { get; set; }

    // Members modal edit-mode (only role is editable)
    private bool IsProjectMemberRoleEditMode { get; set; }
    private Guid EditingProjectMemberIdInModal { get; set; }
    private Guid EditingProjectMemberUserIdInModal { get; set; }
    private DateTime EditingProjectMemberJoinedAtInModal { get; set; }
    private string EditingProjectMemberConcurrencyStampInModal { get; set; } = string.Empty;


    // Create Project Chat modal
    private bool ShowCreateProjectChatModal { get; set; }
    private List<LookupDto<Guid>> SelectedProjectChatMembers { get; set; } = new();
    private Guid? SelectedProjectChatId { get; set; }
    private string CreateChatProjectName { get; set; } = string.Empty;
    private string CreateChatName { get; set; } = string.Empty;
    private ConversationType CreateChatType { get; set; } = ConversationType.Project;


    private CreateProjectConversationInput CreateProjectConversationInput { get; set; } = new();
    public Projects()
    {
        NewProject = new ProjectCreateDto();
        EditingProject = new ProjectUpdateDto();
        Filter = new GetProjectsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        ProjectList = new List<ProjectWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await GetDepartmentCollectionLookupAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Projects"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewProject"], async () => {
            NavigationManager.NavigateTo("/project-detail");
        }, IconName.Add, requiredPolicyName: HCPermissions.Projects.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(ProjectWithNavigationPropertiesDto project)
    {
        DataGridRef?.ToggleDetailRow(project, true);
    }

    private void NavigateToProjectDetail(ProjectWithNavigationPropertiesDto project)
    {
        // Navigate to the project detail page.
        NavigationManager.NavigateTo($"/project-detail/{project.Project.Id}");
    }
    private async Task CreateProjectChatModalAsync(ProjectWithNavigationPropertiesDto project)
    {
        ConversationDto? conversation = null;
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            conversation = await ConversationAppService.FindConversationByProjectIdAsync(project.Project.Id);
            if (conversation == null)
            {
                var members = await ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
                {
                    ProjectId = project.Project.Id,
                    MaxResultCount = 100,
                    SkipCount = 0
                });

                CreateProjectConversationInput = new CreateProjectConversationInput
                {
                    ProjectId = project.Project.Id,
                    Name = project.Project.Name,
                    MemberUserIds = members.Items.Select(m => m.User.Id).ToList()
                };
                conversation = await ConversationAppService.CreateProjectConversationAsync(CreateProjectConversationInput);

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
            }else {
                if(conversation.Members.Any(m => m.UserId == CurrentUser.Id))
                {
                    NavigationManager.NavigateTo($"/chat/{conversation.Id}");
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
            await InvokeAsync(StateHasChanged);
        }
    }
    
    private bool RowSelectableHandler(RowSelectableEventArgs<ProjectWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteProject;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<ProjectWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    protected bool CanManageProject(ProjectDto project)
    {
        return CurrentUser.IsAdminRole()
               || (CurrentUser.Id.HasValue && project.CreatorId == CurrentUser.Id.Value);
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Create);
        CanEditProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Edit);
        CanDeleteProject = await AuthorizationService.IsGrantedAsync(HCPermissions.Projects.Delete);

        CanCreateProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Create);
        CanDeleteProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Delete);
        CanEditProjectMember = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectMembers.Edit);
    }

    private async Task GetProjectsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await ProjectsAppService.GetListAsync(Filter);
        ProjectList = result.Items;
        TotalCount = (int)result.TotalCount;

        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetProjectsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await ProjectsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        // Convert enum to string for Excel download
        var statusFilter = Filter.Status?.ToString();
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/projects/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&Description={HttpUtility.UrlEncode(Filter.Description)}&StartDateMin={Filter.StartDateMin?.ToString("O")}&StartDateMax={Filter.StartDateMax?.ToString("O")}&EndDateMin={Filter.EndDateMin?.ToString("O")}&EndDateMax={Filter.EndDateMax?.ToString("O")}&Status={HttpUtility.UrlEncode(statusFilter)}&OwnerDepartmentId={Filter.OwnerDepartmentId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<ProjectWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetProjectsAsync();
        await InvokeAsync(StateHasChanged);
    }


    
    // Generate next available Project code (Pxxxxxxx format)
    private async Task<string> GenerateNextProjectCodeAsync()
    {
        try
        {
            Logger?.LogInformation("=== GenerateNextProjectCodeAsync: Starting ===");
            int maxNumber = 0;
            const int pageSize = 200; // Process in batches
            int skipCount = 0;
            bool hasMore = true;
            int batchNumber = 0;
            int totalProcessed = 0;
            var foundCodes = new List<string>();
            
            // Query all projects in batches to find the highest "P" code
            while (hasMore)
            {
                batchNumber++;
                var input = new GetProjectsInput
                {
                    MaxResultCount = pageSize,
                    SkipCount = skipCount,
                    Sorting = "Project.Code DESC"
                };
                
                Logger?.LogInformation($"Batch {batchNumber}: Querying with SkipCount={skipCount}, MaxResultCount={pageSize}, Sorting={input.Sorting}");
                var result = await ProjectsAppService.GetListAsync(input);
                
                Logger?.LogInformation($"Batch {batchNumber}: Received {result.Items?.Count ?? 0} items, TotalCount={result.TotalCount}");
                
                if (result.Items == null || result.Items.Count == 0)
                {
                    Logger?.LogInformation($"Batch {batchNumber}: No items found, stopping");
                    hasMore = false;
                    break;
                }
                
                // Iterate through items to find the highest "P" code
                foreach (var project in result.Items)
                {
                    totalProcessed++;
                    if (!string.IsNullOrWhiteSpace(project.Project.Code))
                    {
                        var code = project.Project.Code.Trim();
                        Logger?.LogInformation($"Processing project: Code='{code}'");
                        
                        // Check if code starts with "P" (case-insensitive) and has numeric suffix
                        if (code.StartsWith("P", StringComparison.OrdinalIgnoreCase) && code.Length > 1)
                        {
                            // Extract number part after "P"
                            var numberPart = code.Substring(1);
                            Logger?.LogInformation($"  Code starts with 'P', numberPart='{numberPart}'");
                            
                            if (int.TryParse(numberPart, out int number))
                            {
                                Logger?.LogInformation($"  Parsed number: {number}, current maxNumber: {maxNumber}");
                                foundCodes.Add(code);
                                
                                if (number > maxNumber)
                                {
                                    Logger?.LogInformation($"  *** Updating maxNumber from {maxNumber} to {number} ***");
                                    maxNumber = number;
                                }
                            }
                            else
                            {
                                Logger?.LogInformation($"  Failed to parse numberPart '{numberPart}' as int");
                            }
                        }
                        else
                        {
                            Logger?.LogInformation($"  Code does not start with 'P' or length <= 1");
                        }
                    }
                    else
                    {
                        Logger?.LogInformation($"Project has null or empty Code");
                    }
                }
                
                Logger?.LogInformation($"Batch {batchNumber}: Processed {result.Items.Count} items, maxNumber={maxNumber}, foundCodes=[{string.Join(", ", foundCodes)}]");
                
                // Check if there are more items to process
                if (result.Items.Count < pageSize || skipCount + pageSize >= result.TotalCount)
                {
                    Logger?.LogInformation($"Batch {batchNumber}: No more items (Items.Count={result.Items.Count}, pageSize={pageSize}, skipCount+pageSize={skipCount + pageSize}, TotalCount={result.TotalCount})");
                    hasMore = false;
                }
                else
                {
                    skipCount += pageSize;
                    Logger?.LogInformation($"Batch {batchNumber}: More items available, continuing with skipCount={skipCount}");
                }
            }
            
            var nextCode = $"P{(maxNumber + 1):D7}";
            Logger?.LogInformation($"=== GenerateNextProjectCodeAsync: Finished ===");
            Logger?.LogInformation($"Total processed: {totalProcessed} projects");
            Logger?.LogInformation($"Found codes with 'P' prefix: [{string.Join(", ", foundCodes)}]");
            Logger?.LogInformation($"Max number found: {maxNumber}");
            Logger?.LogInformation($"Generated next code: {nextCode}");
            
            // Generate next code: P + (maxNumber + 1) with 7 digits padding
            return nextCode;
        }
        catch (Exception ex)
        {
            // Fallback to P0000001 if error occurs
            Logger?.LogError(ex, "Error generating project code");
            return "P0000001";
        }
    }

    private async Task CloseCreateProjectModalAsync()
    {
        NewProject = new ProjectCreateDto
        {
            StartDate = DateTime.Now,
            EndDate = DateTime.Now,
        };
        SelectedDepartment = new List<LookupDto<Guid>>();
        SelectedProjectMembers = new List<LookupDto<Guid>>();
        CreatedProjectId = null;
        await CreateProjectModal.Hide();
    }

    private async Task OpenEditProjectModalAsync(ProjectWithNavigationPropertiesDto input)
    {
        try
        {
            SelectedEditTab = "project-edit-tab";
            var project = await ProjectsAppService.GetWithNavigationPropertiesAsync(input.Project.Id);
            EditingProjectId = project.Project.Id;
            
            // Create new instance to ensure Blazor detects the change
            var mappedProject = ObjectMapper.Map<ProjectDto, ProjectUpdateDto>(project.Project);
            
            // Ensure all fields are set properly, create new instance
            EditingProject = new ProjectUpdateDto
            {
                Code = mappedProject?.Code ?? project.Project.Code ?? string.Empty,
                Name = mappedProject?.Name ?? project.Project.Name ?? string.Empty,
                Description = mappedProject?.Description ?? project.Project.Description,
                StartDate = mappedProject?.StartDate ?? project.Project.StartDate,
                EndDate = mappedProject?.EndDate ?? project.Project.EndDate,
                Status = mappedProject?.Status ?? project.Project.Status,
                OwnerDepartmentId = mappedProject?.OwnerDepartmentId ?? project.Project.OwnerDepartmentId,
                ConcurrencyStamp = mappedProject?.ConcurrencyStamp ?? project.Project.ConcurrencyStamp ?? string.Empty
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

            // Reset lazy-load flags for members tab
            SelectedProjectMembers = new List<LookupDto<Guid>>();
            EditingProjectMembersLoadedForProjectId = null;
            IsIdentityUsersLookupLoaded = false;
            IsProjectMembersTabVisited = false;
            
            // Force UI update before showing modal to ensure data is set
            await InvokeAsync(StateHasChanged);

            // Clear validation errors
            EditFieldErrors.Clear();
            EditProjectValidationErrorKey = null;
            
            // Show modal
            await EditProjectModal.Show();
            
            // Force UI update again after showing modal to ensure form is rendered
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OpenProjectTasksModalAsync(ProjectWithNavigationPropertiesDto input)
    {
        try
        {
            ProjectTasksProjectId = input.Project.Id;
            ProjectTasksSorting = string.Empty;
            ProjectTasksCurrentPage = 1;
            ProjectTasksFilterText = null;
            ProjectTasksProjectDisplayName = $"{input.Project.Code} - {input.Project.Name}";
            ProjectTaskChildTasksByParentCode.Clear();
            ExpandedProjectTasks.Clear();
            await LoadProjectTasksAsync(page: ProjectTasksCurrentPage);
            await ProjectTasksModal.Show();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseProjectTasksModalAsync()
    {
        await ProjectTasksModal.Hide();
    }

    private async Task OnProjectTasksGridReadAsync(DataGridReadDataEventArgs<ProjectTaskWithNavigationPropertiesDto> e)
    {
        try
        {
            ProjectTasksCurrentPage = e.Page;
            await LoadProjectTasksAsync(page: ProjectTasksCurrentPage);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task LoadProjectTasksAsync(int page)
    {
        try
        {
            if (ProjectTasksProjectId == Guid.Empty)
            {
                ProjectTasksList = new List<ProjectTaskWithNavigationPropertiesDto>();
                ProjectTasksTotalCount = 0;
                ProjectTaskChildTasksByParentCode = new Dictionary<string, List<ProjectTaskWithNavigationPropertiesDto>>(StringComparer.OrdinalIgnoreCase);
                ExpandedProjectTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var input = new GetProjectTasksInput
            {
                ProjectId = ProjectTasksProjectId,
                OnlyParentTasks = true,
                FilterText = ProjectTasksFilterText,
                MaxResultCount = PageSize,
                SkipCount = (page - 1) * PageSize,
                Sorting = ProjectTasksSorting
            };

            var result = await ProjectTasksAppService.GetListAsync(input);
            ProjectTasksList = result.Items;
            ProjectTasksTotalCount = (int)result.TotalCount;

            await LoadAllProjectTaskChildrenAsync();
            ExpandedProjectTasks.Clear();
            foreach (var task in ProjectTasksList)
            {
                if (task.ChildTaskCount > 0)
                {
                    ExpandedProjectTasks.Add(task.ProjectTask.Code);
                }
            }

            foreach (var task in ProjectTasksList)
            {
                ExpandProjectTaskChildrenRecursively(task.ProjectTask.Code);
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            ProjectTasksList = new List<ProjectTaskWithNavigationPropertiesDto>();
            ProjectTasksTotalCount = 0;
            ProjectTaskChildTasksByParentCode = new Dictionary<string, List<ProjectTaskWithNavigationPropertiesDto>>(StringComparer.OrdinalIgnoreCase);
            ExpandedProjectTasks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private async Task LoadAllProjectTaskChildrenAsync()
    {
        var allChildTasks = new List<ProjectTaskWithNavigationPropertiesDto>();
        var skipCount = 0;
        const int batchSize = 200;
        var hasMore = true;

        while (hasMore)
        {
            var input = new GetProjectTasksInput
            {
                ProjectId = ProjectTasksProjectId,
                OnlyChildTasks = true,
                MaxResultCount = batchSize,
                SkipCount = skipCount,
                Sorting = "ProjectTask.Title ASC"
            };

            var result = await ProjectTasksAppService.GetListAsync(input);
            if (result.Items.Count == 0)
            {
                hasMore = false;
                continue;
            }

            allChildTasks.AddRange(result.Items);
            skipCount += result.Items.Count;
            if (allChildTasks.Count >= result.TotalCount)
            {
                hasMore = false;
            }
        }

        ProjectTaskChildTasksByParentCode = allChildTasks
            .Where(t => !string.IsNullOrWhiteSpace(t.ProjectTask.ParentTaskId))
            .GroupBy(t => t.ProjectTask.ParentTaskId!)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
    }

    private void ExpandProjectTaskChildrenRecursively(string parentCode)
    {
        var childTasks = GetProjectTaskChildrenForParent(parentCode);
        foreach (var childTask in childTasks)
        {
            if (childTask.ChildTaskCount > 0)
            {
                ExpandedProjectTasks.Add(childTask.ProjectTask.Code);
                ExpandProjectTaskChildrenRecursively(childTask.ProjectTask.Code);
            }
        }
    }

    private List<ProjectTaskWithNavigationPropertiesDto> GetProjectTaskChildrenForParent(string parentCode)
    {
        return ProjectTaskChildTasksByParentCode.TryGetValue(parentCode, out var children)
            ? children
            : new List<ProjectTaskWithNavigationPropertiesDto>();
    }

    private async Task SearchProjectTasksAsync()
    {
        ProjectTasksCurrentPage = 1;
        await LoadProjectTasksAsync(ProjectTasksCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshProjectTasksAsync()
    {
        await LoadProjectTasksAsync(ProjectTasksCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private ProjectTaskStatus ParseProjectTaskStatus(string? status)
    {
        return Enum.TryParse<ProjectTaskStatus>(status ?? string.Empty, true, out var parsed)
            ? parsed
            : ProjectTaskStatus.TODO;
    }

    private ProjectTaskPriority ParseProjectTaskPriority(string? priority)
    {
        return Enum.TryParse<ProjectTaskPriority>(priority ?? string.Empty, true, out var parsed)
            ? parsed
            : ProjectTaskPriority.LOW;
    }

    private string GetProjectTaskStatusText(ProjectTaskStatus status) => L[$"Enum:ProjectTaskStatus.{status}"];

    private string GetProjectTaskPriorityText(ProjectTaskPriority priority) => L[$"Enum:ProjectTaskPriority.{priority}"];

    private int GetProjectTaskChildTaskCount(ProjectTaskWithNavigationPropertiesDto task) => task.ChildTaskCount;

    private List<ProjectTaskWithNavigationPropertiesDto> GetProjectTaskChildrenForTask(ProjectTaskWithNavigationPropertiesDto task)
    {
        return GetProjectTaskChildrenForParent(task.ProjectTask.Code);
    }

    private string GetProjectTaskCode(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.Code;

    private string GetProjectTaskTitle(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.Title;

    private DateTime GetProjectTaskStartDate(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.StartDate;

    private DateTime GetProjectTaskDueDate(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.DueDate;

    private ProjectTaskStatus GetProjectTaskStatus(ProjectTaskWithNavigationPropertiesDto task)
    {
        return ParseProjectTaskStatus(task.ProjectTask.Status);
    }

    private ProjectTaskPriority GetProjectTaskPriority(ProjectTaskWithNavigationPropertiesDto task)
    {
        return ParseProjectTaskPriority(task.ProjectTask.Priority);
    }

    private int GetProjectTaskProgressPercent(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.ProgressPercent;

    private string GetProjectTaskDetailUrl(ProjectTaskWithNavigationPropertiesDto task) => $"/project-task-detail/{task.ProjectTask.Id}";

    private string GetProjectTaskParentTaskId(ProjectTaskWithNavigationPropertiesDto task) => task.ProjectTask.ParentTaskId ?? string.Empty;

    private string GetProjectTaskParentTaskTitle(ProjectTaskWithNavigationPropertiesDto task) => task.ParentTaskTitle ?? string.Empty;

    private Color GetProjectTaskStatusBadgeColor(ProjectTaskStatus status)
    {
        return EnumStatusColorHelper.GetProjectTaskStatusBadgeColor(status);
    }

    private Color GetProjectTaskPriorityBadgeColor(ProjectTaskPriority priority)
    {
        return EnumStatusColorHelper.GetProjectTaskPriorityBadgeColor(priority);
    }

    private async Task HandleProjectTaskExpandedAsync((string TaskCode, bool IsExpanded) eventArgs)
    {
        if (eventArgs.IsExpanded)
        {
            ExpandedProjectTasks.Add(eventArgs.TaskCode);
        }
        else
        {
            ExpandedProjectTasks.Remove(eventArgs.TaskCode);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenProjectMembersModalAsync(ProjectWithNavigationPropertiesDto input)
    {
        try
        {
            ProjectMembersProjectId = input.Project.Id;
            ProjectMembersSorting = string.Empty;
            ProjectMembersCurrentPage = 1;
            ProjectMembersToAdd = new List<LookupDto<Guid>>();
            ProjectMembersRoleToAdd = ProjectMemberRole.MEMBER;
            ProjectMembersFilterText = null;
            ProjectMembersProjectDisplayName = $"{input.Project.Code} - {input.Project.Name}";

            IsProjectMemberRoleEditMode = false;
            EditingProjectMemberIdInModal = Guid.Empty;
            EditingProjectMemberUserIdInModal = Guid.Empty;
            EditingProjectMemberConcurrencyStampInModal = string.Empty;

            // Preload user lookup for add-member UI
            if (CanCreateProjectMember)
            {
                await GetIdentityUserCollectionLookupAsync();
            }

            await LoadProjectMembersListAsync(page: 1);
            await ProjectMembersModal.Show();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseProjectMembersModalAsync()
    {
        await ProjectMembersModal.Hide();
    }

    private async Task OnProjectMembersGridReadAsync(DataGridReadDataEventArgs<ProjectMemberWithNavigationPropertiesDto> e)
    {
        ProjectMembersSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default)
            .Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : ""))
            .JoinAsString(",");

        ProjectMembersCurrentPage = e.Page;
        await LoadProjectMembersListAsync(page: ProjectMembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task LoadProjectMembersListAsync(int page)
    {
        var input = new GetProjectMembersInput
        {
            FilterText = ProjectMembersFilterText,
            ProjectId = ProjectMembersProjectId,
            MaxResultCount = PageSize,
            SkipCount = (page - 1) * PageSize,
            Sorting = ProjectMembersSorting
        };

        var result = await ProjectMembersAppService.GetListAsync(input);
        ProjectMembersList = result.Items;
        ProjectMembersTotalCount = (int)result.TotalCount;
    }

    private async Task SearchProjectMembersAsync()
    {
        ProjectMembersCurrentPage = 1;
        await LoadProjectMembersListAsync(ProjectMembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private async Task RefreshProjectMembersAsync()
    {
        await LoadProjectMembersListAsync(ProjectMembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    protected virtual void OnProjectMembersToAddChanged()
    {
        if (IsProjectMemberRoleEditMode)
        {
            return;
        }

        // Select2 (single-select) may mutate the list in-place; force re-render so the Add button enables.
        InvokeAsync(StateHasChanged);
    }

    private async Task AddProjectMembersAsync()
    {
        if (ProjectMembersProjectId == Guid.Empty)
        {
            return;
        }

        if (IsProjectMemberRoleEditMode)
        {
            await UpdateProjectMemberRoleAsync();
            return;
        }

        if (!CanCreateProjectMember)
        {
            return;
        }

        if (ProjectMembersToAdd == null || ProjectMembersToAdd.Count == 0)
        {
            return;
        }

        foreach (var user in ProjectMembersToAdd)
        {
            try
            {
                // Avoid duplicate adds with a cheap existence check
                var exists = await ProjectMembersAppService.GetListAsync(new GetProjectMembersInput
                {
                    ProjectId = ProjectMembersProjectId,
                    UserId = user.Id,
                    MaxResultCount = 1
                });

                if (exists.TotalCount > 0)
                {
                    continue;
                }

                await ProjectMembersAppService.CreateAsync(new ProjectMemberCreateDto
                {
                    ProjectId = ProjectMembersProjectId,
                    UserId = user.Id,
                    MemberRole = ProjectMembersRoleToAdd,
                    JoinedAt = DateTime.Now
                });
            }
            catch (Exception ex)
            {
                // Keep going for other users; show error once per failure
                await HandleErrorAsync(ex);
            }
        }

        ProjectMembersToAdd = new List<LookupDto<Guid>>();
        await LoadProjectMembersListAsync(ProjectMembersCurrentPage);
        await InvokeAsync(StateHasChanged);
    }

    private void CancelProjectMemberRoleEdit()
    {
        IsProjectMemberRoleEditMode = false;
        EditingProjectMemberIdInModal = Guid.Empty;
        EditingProjectMemberUserIdInModal = Guid.Empty;
        EditingProjectMemberConcurrencyStampInModal = string.Empty;

        ProjectMembersToAdd = new List<LookupDto<Guid>>();
        ProjectMembersRoleToAdd = ProjectMemberRole.MEMBER;

        InvokeAsync(StateHasChanged);
    }

    private async Task ToggleEditProjectMemberRoleAsync(ProjectMemberWithNavigationPropertiesDto row)
    {
        if (!CanEditProjectMember)
        {
            return;
        }

        if (IsProjectMemberRoleEditMode && EditingProjectMemberIdInModal == row.ProjectMember.Id)
        {
            CancelProjectMemberRoleEdit();
            return;
        }

        // Enter edit mode: fill user + role, disable user select
        IsProjectMemberRoleEditMode = true;
        EditingProjectMemberIdInModal = row.ProjectMember.Id;
        EditingProjectMemberUserIdInModal = row.ProjectMember.UserId;
        EditingProjectMemberJoinedAtInModal = row.ProjectMember.JoinedAt;
        EditingProjectMemberConcurrencyStampInModal = row.ProjectMember.ConcurrencyStamp ?? string.Empty;

        ProjectMembersRoleToAdd = row.ProjectMember.MemberRole;

        // Fill select2 value (single-select uses a list)
        var displayName = (row.User?.Surname + " " + row.User?.Name) ?? string.Empty;
        ProjectMembersToAdd = new List<LookupDto<Guid>> { new() { Id = row.ProjectMember.UserId, DisplayName = displayName } };

        // Ensure selected user exists in datasource so Select2 can render it
        if (!IdentityUsersCollection.Any(x => x.Id == row.ProjectMember.UserId))
        {
            IdentityUsersCollection = IdentityUsersCollection.Concat(ProjectMembersToAdd).ToList();
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task UpdateProjectMemberRoleAsync()
    {
        if (!CanEditProjectMember || !IsProjectMemberRoleEditMode || EditingProjectMemberIdInModal == Guid.Empty)
        {
            return;
        }

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await ProjectMembersAppService.UpdateAsync(EditingProjectMemberIdInModal, new ProjectMemberUpdateDto
            {
                ProjectId = ProjectMembersProjectId,
                UserId = EditingProjectMemberUserIdInModal,
                MemberRole = ProjectMembersRoleToAdd,
                JoinedAt = EditingProjectMemberJoinedAtInModal,
                ConcurrencyStamp = EditingProjectMemberConcurrencyStampInModal
            });

            await LoadProjectMembersListAsync(ProjectMembersCurrentPage);
            CancelProjectMemberRoleEdit();
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

    private async Task DeleteProjectMemberAsync(ProjectMemberWithNavigationPropertiesDto input)
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
            await LoadProjectMembersListAsync(ProjectMembersCurrentPage);
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

    private async Task ShowUIBlockAsync()
    {
        await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
    }

    private async Task DeleteProjectAsync(ProjectWithNavigationPropertiesDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await ProjectsAppService.DeleteAsync(input.Project.Id);
            await GetProjectsAsync();
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

    private async Task DeleteProjectWithConfirmationAsync(ProjectWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteProjectAsync(input);
        }
    }

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

        // Required: Status
        // Status is enum, so we check if it's a valid value (not default if it's required)
        // Assuming ProjectStatus has a default value that means "not set"
        // You may need to adjust this based on your enum definition

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
            CreatedProjectId = createdProject.Id;
            await GetProjectsAsync();
            await CloseCreateProjectModalAsync();
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
    
    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }
    
    private async Task<List<LookupDto<Guid>>> GetIdentityUserCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        IdentityUsersCollection = (await ProjectMembersAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return IdentityUsersCollection.ToList();
    }
    
    private async Task LoadProjectMembersAsync(Guid projectId)
    {
        // Load all members using pagination - start with first batch to get total count
        var firstInput = new GetProjectMembersInput 
        { 
            ProjectId = projectId, 
            MaxResultCount = PageSize,
            SkipCount = 0
        };
        var firstResult = await ProjectMembersAppService.GetListAsync(firstInput);
        var allMembers = new List<ProjectMemberWithNavigationPropertiesDto>(firstResult.Items);
        
        // Load remaining members if any
        if (firstResult.TotalCount > PageSize)
        {
            var skipCount = PageSize;
            while (skipCount < firstResult.TotalCount)
            {
                var input = new GetProjectMembersInput 
                { 
                    ProjectId = projectId, 
                    MaxResultCount = PageSize,
                    SkipCount = skipCount
                };
                var result = await ProjectMembersAppService.GetListAsync(input);
                allMembers.AddRange(result.Items);
                skipCount += PageSize;
                
                if (result.Items.Count < PageSize)
                {
                    break;
                }
            }
        }
        
        SelectedProjectMembers = allMembers.Select(x => new LookupDto<Guid> { Id = x.ProjectMember.UserId, DisplayName = x.User?.UserName ?? "" }).ToList();
    }
    
    private async Task SaveProjectMembersAsync(Guid projectId)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            // Load all existing members using pagination
            var firstInput = new GetProjectMembersInput 
            { 
                ProjectId = projectId, 
                MaxResultCount = PageSize,
                SkipCount = 0
            };
            var firstResult = await ProjectMembersAppService.GetListAsync(firstInput);
            var allExistingMembers = new List<ProjectMemberWithNavigationPropertiesDto>(firstResult.Items);
            
            // Load remaining members if any
            if (firstResult.TotalCount > PageSize)
            {
                var skipCount = PageSize;
                while (skipCount < firstResult.TotalCount)
                {
                    var input = new GetProjectMembersInput 
                    { 
                        ProjectId = projectId, 
                        MaxResultCount = PageSize,
                        SkipCount = skipCount
                    };
                    var result = await ProjectMembersAppService.GetListAsync(input);
                    allExistingMembers.AddRange(result.Items);
                    skipCount += PageSize;
                    
                    if (result.Items.Count < PageSize)
                    {
                        break;
                    }
                }
            }
            
            var existingUserIds = allExistingMembers.Select(x => x.ProjectMember.UserId).ToHashSet();
            
            // Get selected user IDs
            var selectedUserIds = SelectedProjectMembers.Select(x => x.Id).ToHashSet();
            
            // Delete members that are not in selected list
            var membersToDelete = allExistingMembers.Where(x => !selectedUserIds.Contains(x.ProjectMember.UserId)).ToList();
            foreach (var member in membersToDelete)
            {
                await ProjectMembersAppService.DeleteAsync(member.ProjectMember.Id);
            }
            
            // Add new members that are not in existing list
            var membersToAdd = SelectedProjectMembers.Where(x => !existingUserIds.Contains(x.Id)).ToList();
            foreach (var user in membersToAdd)
            {
                await ProjectMembersAppService.CreateAsync(new ProjectMemberCreateDto
                {
                    ProjectId = projectId,
                    UserId = user.Id,
                    MemberRole = ProjectMemberRole.MEMBER,
                    JoinedAt = DateTime.Now
                });
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

    private async Task CloseEditProjectModalAsync()
    {
        await EditProjectModal.Hide();
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
            await ProjectsAppService.UpdateAsync(EditingProjectId, EditingProject);
            
            // Save project members if members tab was visited (members may be edited then user returns to main tab)
            if (IsProjectMembersTabVisited)
            {
                await SaveProjectMembersAsync(EditingProjectId);
            }
            
            await GetProjectsAsync();
            await EditProjectModal.Hide();
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

    private void OnSelectedCreateTabChanged(string name)
    {
        SelectedCreateTab = name;
    }

    private async Task OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;

        if (name == "project-members-tab")
        {
            await EnsureEditProjectMembersDataLoadedAsync();
        }
    }

    private async Task EnsureEditProjectMembersDataLoadedAsync()
    {
        if (EditingProjectId == Guid.Empty)
        {
            return;
        }

        IsProjectMembersTabVisited = true;

        if (EditingProjectMembersLoadedForProjectId != EditingProjectId)
        {
            await LoadProjectMembersAsync(EditingProjectId);
            EditingProjectMembersLoadedForProjectId = EditingProjectId;
        }

        // Preload identity users for better UX (Select2 will still filter on demand)
        if (!IsIdentityUsersLookupLoaded)
        {
            await GetIdentityUserCollectionLookupAsync();
            IsIdentityUsersLookupLoaded = true;
        }

        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task OnCodeChangedAsync(string? code)
    {
        Filter.Code = code;
        await SearchAsync();
    }

    protected virtual async Task OnNameChangedAsync(string? name)
    {
        Filter.Name = name;
        await SearchAsync();
    }

    protected virtual async Task OnDescriptionChangedAsync(string? description)
    {
        Filter.Description = description;
        await SearchAsync();
    }

    protected virtual async Task OnStartDateMinChangedAsync(DateTime? startDateMin)
    {
        Filter.StartDateMin = startDateMin.HasValue ? startDateMin.Value.Date : startDateMin;
        await SearchAsync();
    }

    protected virtual async Task OnStartDateMaxChangedAsync(DateTime? startDateMax)
    {
        Filter.StartDateMax = startDateMax.HasValue ? startDateMax.Value.Date.AddDays(1).AddSeconds(-1) : startDateMax;
        await SearchAsync();
    }

    protected virtual async Task OnEndDateMinChangedAsync(DateTime? endDateMin)
    {
        Filter.EndDateMin = endDateMin.HasValue ? endDateMin.Value.Date : endDateMin;
        await SearchAsync();
    }

    protected virtual async Task OnEndDateMaxChangedAsync(DateTime? endDateMax)
    {
        Filter.EndDateMax = endDateMax.HasValue ? endDateMax.Value.Date.AddDays(1).AddSeconds(-1) : endDateMax;
        await SearchAsync();
    }

    protected virtual async Task OnStatusChangedAsync(string? status)
    {
        Filter.Status = Enum.TryParse<ProjectStatus>(status, true, out var parsedStatus)
            ? parsedStatus
            : null;
        StatusFilterValue = status ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnOwnerDepartmentIdChangedAsync(string? ownerDepartmentId)
    {
        Filter.OwnerDepartmentId = Guid.TryParse(ownerDepartmentId, out var parsedDepartmentId)
            ? parsedDepartmentId
            : null;
        OwnerDepartmentFilterValue = ownerDepartmentId ?? string.Empty;
        await SearchAsync();
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
    
    protected virtual void OnProjectMembersChanged(List<LookupDto<Guid>> value)
    {
        SelectedProjectMembers = value ?? new List<LookupDto<Guid>>();
    }

    private Task SelectAllItems()
    {
        AllProjectsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllProjectsSelected = false;
        SelectedProjects.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedProjectRowsChanged()
    {
        if (SelectedProjects.Count != PageSize)
        {
            AllProjectsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedProjectsAsync()
    {
        try
        {
            var message = AllProjectsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedProjects.Count].Value;
            if (!await UiMessageService.Confirm(message))
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            if (AllProjectsSelected)
            {
                await ProjectsAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await ProjectsAppService.DeleteByIdsAsync(SelectedProjects.Select(x => x.Project.Id).ToList());
            }

            SelectedProjects.Clear();
            AllProjectsSelected = false;
            await GetProjectsAsync();
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
