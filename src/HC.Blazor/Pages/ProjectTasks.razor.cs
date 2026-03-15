using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Web;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.ProjectTasks;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using System.Threading;
using Volo.Abp.Identity;
using HC.DocumentFiles;
using Volo.Abp.BlobStoring;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.Users;
using HC.Chat.Helpers;  

namespace HC.Blazor.Pages;

public partial class ProjectTasks
{
    [Inject] private IProjectTaskAssignmentsAppService ProjectTaskAssignmentsAppService { get; set; } = default!;
    [Inject] private IProjectTaskDocumentsAppService ProjectTaskDocumentsAppService { get; set; } = default!;
    [Inject] private IDocumentFilesAppService DocumentFilesAppService { get; set; } = default!;
    [Inject] private IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] private HC.DocumentPdfViewer.IDocumentPdfViewerAppService DocumentPdfViewerAppService { get; set; } = default!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Kanban UI
    protected bool IsKanbanView { get; set; } = true;
    protected bool ShowCancelledLane { get; set; }
    private bool IsKanbanLoadedOnce { get; set; }
    protected int KanbanRenderKey { get; set; }
    protected bool IsKanbanUpdating { get; set; }

    private const int KanbanItemsPerColumn = 10; // PageSize per status
    
    // Track pagination per status (Page and PageSize)
    private Dictionary<ProjectTaskStatus, int> KanbanPages { get; set; } = new();
    private Dictionary<ProjectTaskStatus, int> KanbanPageSizes { get; set; } = new();
    
    // Track loaded items count per status
    private Dictionary<ProjectTaskStatus, int> KanbanLoadedCounts { get; set; } = new();
    private Dictionary<ProjectTaskStatus, int> KanbanTotalCounts { get; set; } = new();
    
    // Track loading state per status
    private Dictionary<ProjectTaskStatus, bool> KanbanLoadingStates { get; set; } = new();
    
    // Store all loaded kanban items (not just displayed ones)
    private List<KanbanItem> AllKanbanItems { get; set; } = new();

    protected sealed class KanbanItem
    {
        public Guid Id { get; init; }
        public string ProjectName { get; set; } = string.Empty;
        public string? ParentTaskCode { get; set; }
        public string? ParentTaskTitle { get; set; }
        public int ChildTaskCount { get; set; }
        public string Code { get; init; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public ProjectTaskPriority Priority { get; set; } = ProjectTaskPriority.LOW;
        public DateTime? DueDate { get; set; }
        public ProjectTaskStatus Status { get; set; }
        public int ProgressPercent { get; set; }
        public List<IdentityUserDto> Assignees { get; set; } = new();
        public int DocumentsCount { get; set; }

        // Keep the full DTO so we can update via AppService.
        public ProjectTaskDto ProjectTask { get; init; } = null!;

        // Keep navigation DTO for edit/delete actions from Kanban card.
        public ProjectTaskWithNavigationPropertiesDto ProjectTaskWithNavigationProperties { get; init; } = null!;
    }

    protected List<KanbanItem> KanbanItems { get; set; } = new();

    // Create task modal helpers (Select2 + Enum selects)
    private List<LookupDto<Guid>> SelectedNewProjectTaskProject { get; set; } = new();

    protected sealed class ParentTaskSelectItem
    {
        public string Id { get; init; } = string.Empty;
        public string DisplayName { get; init; } = string.Empty;
    }

    private IReadOnlyList<ParentTaskSelectItem> ParentTasksCollection { get; set; } = new List<ParentTaskSelectItem>();
    private List<ParentTaskSelectItem> SelectedFilterParentTask { get; set; } = new();
    private List<ParentTaskSelectItem> SelectedNewProjectTaskParentTask { get; set; } = new();
    private IReadOnlyList<ParentTaskSelectItem> EditParentTasksCollection { get; set; } = new List<ParentTaskSelectItem>();
    private Guid EditParentTaskSelectKey { get; set; } = Guid.NewGuid();

    private ProjectTaskPriority NewProjectTaskPriority { get; set; } = ProjectTaskPriority.LOW;
    private ProjectTaskStatus NewProjectTaskStatus { get; set; } = ProjectTaskStatus.TODO;

    private DatePicker<DateTime>? NewProjectTaskStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? NewProjectTaskDueDateDatePicker { get; set; }

    // Create wizard state (General -> Assignments -> Documents)
    private Guid CreateWizardProjectTaskId { get; set; }
    protected bool IsCreateWizardGeneralSaved => CreateWizardProjectTaskId != Guid.Empty;
    private string? CreateGeneralValidationErrorKey { get; set; }
    
    // Loading states for better UX
    private bool IsSavingGeneralInformation { get; set; }
    private bool IsFinishingWizard { get; set; }
    private bool IsUpdatingProjectTask { get; set; }
    private bool IsNavigatingTab { get; set; }
    private CancellationTokenSource? ProgressFilterSearchCts { get; set; }
    
    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();
    
    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);

    // Assignments (create wizard)
    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> CreateAssignmentsList { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> AssignmentIdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> CreateAssignmentsUsersToAdd { get; set; } = new();
    private ProjectTaskAssignmentRole CreateAssignmentRole { get; set; } = ProjectTaskAssignmentRole.MAIN;
    private string? CreateAssignmentNote { get; set; }

    // Assignments (edit modal)
    private IReadOnlyList<ProjectTaskAssignmentWithNavigationPropertiesDto> EditAssignmentsList { get; set; } = new List<ProjectTaskAssignmentWithNavigationPropertiesDto>();
    private List<LookupDto<Guid>> EditAssignmentsUsersToAdd { get; set; } = new();
    private ProjectTaskAssignmentRole EditAssignmentRole { get; set; } = ProjectTaskAssignmentRole.MAIN;
    private string? EditAssignmentNote { get; set; }

    // Documents (create wizard)
    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> CreateDocumentsList { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private IReadOnlyList<LookupDto<Guid>> DocumentsLookupCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> CreateDocumentsToAdd { get; set; } = new();
    private ProjectTaskDocumentPurpose CreateDocumentPurpose { get; set; } = ProjectTaskDocumentPurpose.REPORT;

    // Documents (edit modal)
    private IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> EditDocumentsList { get; set; } = new List<ProjectTaskDocumentWithNavigationPropertiesDto>();
    private List<LookupDto<Guid>> EditDocumentsToAdd { get; set; } = new();
    private ProjectTaskDocumentPurpose EditDocumentPurpose { get; set; } = ProjectTaskDocumentPurpose.REPORT;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; set;} = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<ProjectTaskWithNavigationPropertiesDto>? DataGridRef { get; set; }

    private IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> ProjectTaskList { get; set; }

    private int PageSize { get; } = 10;//LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = "ProjectTask.CreationTime DESC";
    private int TotalCount { get; set; }

    private bool CanCreateProjectTask { get; set; }

    private bool CanEditProjectTask { get; set; }

    private bool CanDeleteProjectTask { get; set; }

    private bool CanCreateProjectTaskAssignment { get; set; }
    private bool CanEditProjectTaskAssignment { get; set; }
    private bool CanDeleteProjectTaskAssignment { get; set; }

    private ProjectTaskDto NewProjectTask { get; set; }
    private ProjectTaskUpdateDto EditingProjectTask { get; set; }
    private Guid EditingProjectTaskId { get; set; }
    private string? EditGeneralValidationErrorKey { get; set; }
    private DatePicker<DateTime>? EditProjectTaskStartDateDatePicker { get; set; }
    private DatePicker<DateTime>? EditProjectTaskDueDateDatePicker { get; set; }

    // Edit task modal helpers (Select2 + Enum selects)
    private List<LookupDto<Guid>> SelectedEditProjectTaskProject { get; set; } = new();
    private List<ParentTaskSelectItem> SelectedEditProjectTaskParentTask { get; set; } = new();
    private ProjectTaskPriority EditingProjectTaskPriority { get; set; } = ProjectTaskPriority.LOW;
    private ProjectTaskStatus EditingProjectTaskStatus { get; set; } = ProjectTaskStatus.TODO;

    private Modal EditProjectTaskModal { get; set; } = new();
    
    // Reference to the ProjectTaskCreateModal component
    private HC.Blazor.Components.ProjectTaskCreateModal.ProjectTaskCreateModal? ProjectTaskCreateModalRef { get; set; }
    private GetProjectTasksInput Filter { get; set; }

    private DataGridEntityActionsColumn<ProjectTaskWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "general";
    protected string SelectedEditTab = "general";

    private IReadOnlyList<LookupDto<Guid>> ProjectsCollection { get; set; } = new List<LookupDto<Guid>>();
    private string ProjectFilterValue { get; set; } = string.Empty;
    private List<ProjectTaskWithNavigationPropertiesDto> SelectedProjectTasks { get; set; } = new();
    private bool AllProjectTasksSelected { get; set; }
    
    // PDF viewer
    private string? PdfFileUrl { get; set; }
    private bool IsPdfFile { get; set; }
    private Modal? PdfViewerModal { get; set; }
    
    // Track which modal was open before opening PDF viewer
    private bool WasCreateModalOpen { get; set; }
    private bool WasEditModalOpen { get; set; }
    
    // Cache PDF file info for documents (key: DocumentId, value: has PDF file)
    private Dictionary<Guid, bool> DocumentHasPdfCache { get; set; } = new();

    public ProjectTasks()
    {
        NewProjectTask = new ProjectTaskDto();
        EditingProjectTask = new ProjectTaskUpdateDto();
        Filter = new GetProjectTasksInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        ProjectTaskList = new List<ProjectTaskWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetBreadcrumbItemsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetPermissionsAsync();
            await SetToolbarItemsAsync();
            await GetProjectCollectionLookupAsync();
            await RefreshKanbanAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["ProjectTasks"]));
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
        Toolbar.AddButton(
            IsKanbanView ? L["List"] : L["Kanban"],
            async () => { await ToggleViewAsync(); },
            IsKanbanView ? IconName.List : IconName.GripVertical);

        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);

        Toolbar.AddButton(L["NewTask"], async () => {
            await OpenCreateProjectTaskModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.ProjectTasks.Create);
    }

    private async Task ToggleViewAsync()
    {
        // Flip view first so toolbar text/icon updates immediately.
        IsKanbanView = !IsKanbanView;
        RebuildToolbar();
        await InvokeAsync(StateHasChanged);

        if (IsKanbanView)
        {
            await RefreshKanbanAsync();
        }
        else
        {
            await GetProjectTasksAsync();
        }

        // Ensure toolbar and view are synced after data load.
        RebuildToolbar();
        await InvokeAsync(StateHasChanged);
    }

    private void ToggleDetails(ProjectTaskWithNavigationPropertiesDto projectTask)
    {
        DataGridRef?.ToggleDetailRow(projectTask, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<ProjectTaskWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteProjectTask;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<ProjectTaskWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Create);
        CanEditProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Edit);
        CanDeleteProjectTask = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTasks.Delete);

        CanCreateProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Create);
        CanEditProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Edit);
        CanDeleteProjectTaskAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.ProjectTaskAssignments.Delete);
    }

    protected string GetStatusText(ProjectTaskStatus status)
    {
        // Uses ABP localization keys defined in en.json/vi.json.
        return L[$"Enum:ProjectTaskStatus.{status}"];
    }

    // DTO stores enums as string; parse safely for UI rendering.
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


    protected Color GetPriorityBadgeColor(ProjectTaskPriority priority)
    {
        return priority switch
        {
            ProjectTaskPriority.LOW => Color.Secondary,
            ProjectTaskPriority.MEDIUM => Color.Info,
            ProjectTaskPriority.HIGH => Color.Warning,
            ProjectTaskPriority.URGENT => Color.Danger,
            _ => Color.Secondary,
        };
    }

    protected Color GetStatusBadgeColor(ProjectTaskStatus status)
    {
        return status switch
        {
            ProjectTaskStatus.TODO => Color.Secondary,
            ProjectTaskStatus.IN_PROGRESS => Color.Primary,
            ProjectTaskStatus.WAITING => Color.Warning,
            ProjectTaskStatus.DONE => Color.Success,
            ProjectTaskStatus.CANCELLED => Color.Danger,
            _ => Color.Secondary,
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

    protected bool CanEditTask(ProjectTaskWithNavigationPropertiesDto task)
    {
        if (!CanEditProjectTask)
        {
            return false;
        }

        if (CurrentUser.IsAdminRole())
        {
            return true;
        }

        if (!CurrentUser.Id.HasValue)
        {
            return false;
        }

        if (task.ProjectTask.CreatorId == CurrentUser.Id.Value)
        {
            return true;
        }

        return task.ProjectTaskAssignments.Any(x => x.ProjectTaskAssignment.UserId == CurrentUser.Id.Value);
    }

    // Check if current user can delete the task (creator or admin)
    protected bool CanDeleteTask(ProjectTaskDto task)
    {
        return CanDeleteProjectTask
               && CurrentUser.Id != null
               && (CurrentUser.Id.Equals(task.CreatorId) || CurrentUser.IsAdminRole());
    }

    protected string GetPriorityText(ProjectTaskPriority priority)
    {
        return L[$"Enum:ProjectTaskPriority.{priority}"];
    }

    protected Task OnKanbanItemDropped(DraggableDroppedEventArgs<KanbanItem> args)
    {
        return OnKanbanItemDroppedAsync(args);
    }

    private async Task OnKanbanItemDroppedAsync(DraggableDroppedEventArgs<KanbanItem> args)
    {
        if (IsKanbanUpdating)
        {
            return;
        }

        if (args.Item is null)
        {
            return;
        }

        if (!Enum.TryParse<ProjectTaskStatus>(args.DropZoneName, ignoreCase: true, out var newStatus))
        {
            return;
        }

        if (args.Item.Status == newStatus)
        {
            return;
        }

        IsKanbanUpdating = true;
        await InvokeAsync(StateHasChanged);

        try
        {
            await UpdateProjectTaskStatusAsync(args.Item, newStatus);
            // UpdateDisplayedKanbanItems() is already called in UpdateProjectTaskStatusAsync
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsKanbanUpdating = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task UpdateProjectTaskStatusAsync(KanbanItem item, ProjectTaskStatus newStatus)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            // Store old status to update counts
            var oldStatus = item.Status;

            // Always fetch the latest task snapshot from server to avoid stale ConcurrencyStamp.
            var latestTask = await ProjectTasksAppService.GetAsync(item.ProjectTask.Id);
            
            // Auto-set ProgressPercent to 100 when task is moved to Done
            var progressPercent = latestTask.ProgressPercent;
            if (newStatus == ProjectTaskStatus.DONE)
            {
                progressPercent = 100;
            }
            
            var input = new ProjectTaskUpdateDto
            {
                ParentTaskId = latestTask.ParentTaskId,
                Code = latestTask.Code,
                Title = latestTask.Title,
                Description = latestTask.Description,
                StartDate = latestTask.StartDate,
                DueDate = latestTask.DueDate,
                Priority = latestTask.Priority,
                Status = newStatus.ToString(),
                ProgressPercent = progressPercent,
                ProjectId = latestTask.ProjectId,
                ConcurrencyStamp = latestTask.ConcurrencyStamp
            };

            var updatedTask = await ProjectTasksAppService.UpdateAsync(item.ProjectTask.Id, input);

            // Update local state after the server call succeeds.
            item.ProjectTask.Status = input.Status;
            item.ProjectTask.ProgressPercent = input.ProgressPercent;
            item.ProjectTask.ConcurrencyStamp = updatedTask.ConcurrencyStamp;
            item.Status = newStatus;
            item.ProgressPercent = input.ProgressPercent;
            
            // Update AllKanbanItems to reflect the status and progress change
            var allItem = AllKanbanItems.FirstOrDefault(x => x.Id == item.Id);
            if (allItem != null)
            {
                allItem.Status = newStatus;
                allItem.ProjectTask.Status = input.Status;
                allItem.ProjectTask.ProgressPercent = input.ProgressPercent;
                allItem.ProjectTask.ConcurrencyStamp = updatedTask.ConcurrencyStamp;
                allItem.ProgressPercent = input.ProgressPercent;
                if (allItem.ProjectTaskWithNavigationProperties?.ProjectTask is not null)
                {
                    allItem.ProjectTaskWithNavigationProperties.ProjectTask.ConcurrencyStamp = updatedTask.ConcurrencyStamp;
                }
            }
            
            // Update total counts locally instead of querying API
            if (oldStatus != newStatus)
            {
                // Decrease count for old status
                if (KanbanTotalCounts.ContainsKey(oldStatus))
                {
                    KanbanTotalCounts[oldStatus] = Math.Max(0, KanbanTotalCounts[oldStatus] - 1);
                }
                if (KanbanLoadedCounts.ContainsKey(oldStatus))
                {
                    KanbanLoadedCounts[oldStatus] = Math.Max(0, KanbanLoadedCounts[oldStatus] - 1);
                }
                
                // Increase count for new status
                if (KanbanTotalCounts.ContainsKey(newStatus))
                {
                    KanbanTotalCounts[newStatus] = KanbanTotalCounts[newStatus] + 1;
                }
                if (KanbanLoadedCounts.ContainsKey(newStatus))
                {
                    KanbanLoadedCounts[newStatus] = KanbanLoadedCounts[newStatus] + 1;
                }
            }
            
            // Refresh displayed items
            UpdateDisplayedKanbanItems();
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

    private async Task RefreshKanbanAsync()
    {
        // Reset all kanban state
        KanbanPages.Clear();
        KanbanPageSizes.Clear();
        KanbanLoadedCounts.Clear();
        KanbanTotalCounts.Clear();
        KanbanLoadingStates.Clear();
        AllKanbanItems.Clear();
        
        // Determine which statuses to load based on filter
        var allStatuses = Enum.GetValues<ProjectTaskStatus>().ToArray();
        var statusesToLoad = allStatuses;
        
        // If status filter is specified, only load that status
        if (!string.IsNullOrWhiteSpace(Filter.Status))
        {
            if (Enum.TryParse<ProjectTaskStatus>(Filter.Status, ignoreCase: true, out var filteredStatus))
            {
                statusesToLoad = new[] { filteredStatus };
            }
        }
        
        foreach (var status in allStatuses)
        {
            KanbanPages[status] = 1;
            KanbanPageSizes[status] = KanbanItemsPerColumn;
            KanbanLoadingStates[status] = true; // Set loading state to true
        }
        
        // Notify UI that loading has started
        await InvokeAsync(StateHasChanged);
        
        // Load first page for each status in parallel (only for statuses to load)
        var loadTasks = statusesToLoad.Select(status => 
            LoadKanbanItemsForStatusAsync(status, isInitialLoad: true)
        ).ToArray();
        
        await Task.WhenAll(loadTasks);
        
        // Set all loading states to false after all loads complete
        foreach (var status in allStatuses)
        {
            KanbanLoadingStates[status] = false;
        }
        
        UpdateDisplayedKanbanItems();
        IsKanbanLoadedOnce = true;
        KanbanRenderKey++;
        await InvokeAsync(StateHasChanged);
    }
    
    private async Task<int> LoadKanbanItemsForStatusAsync(ProjectTaskStatus status, bool isInitialLoad = false)
    {
        try
        {
        var currentPage = KanbanPages.GetValueOrDefault(status, 1);
        var pageSize = KanbanPageSizes.GetValueOrDefault(status, KanbanItemsPerColumn);
        var skipCount = (currentPage - 1) * pageSize;
        
        // Query with pagination
        var input = new GetProjectTasksInput
        {
            FilterText = Filter.FilterText,
            ParentTaskId = Filter.ParentTaskId,
            Code = Filter.Code,
            Title = Filter.Title,
            Description = Filter.Description,
            StartDateMin = Filter.StartDateMin,
            StartDateMax = Filter.StartDateMax,
            DueDateMin = Filter.DueDateMin,
            DueDateMax = Filter.DueDateMax,
            Priority = Filter.Priority,
            Status = status.ToString(),
            ProgressPercentMin = Filter.ProgressPercentMin,
            ProgressPercentMax = Filter.ProgressPercentMax,
            ProjectId = Filter.ProjectId,
            SkipCount = skipCount,
            MaxResultCount = pageSize,
            Sorting = "ProjectTask.CreationTime DESC"
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        var allItems = result.Items.Select(dto => MapToKanbanItem(dto, status)).ToList();
        var totalCount = result.TotalCount;
        
        // Remove duplicates from allItems (in case API returns duplicates)
        var uniqueNewItems = allItems
            .GroupBy(x => x.Id)
            .Select(g => g.First())
            .ToList();
        
        // Remove any existing items with same Id before adding to prevent duplicates
        var existingIds = AllKanbanItems.Select(x => x.Id).ToHashSet();
        var itemsToAdd = uniqueNewItems.Where(x => !existingIds.Contains(x.Id)).ToList();
        
        if (isInitialLoad)
        {
            // Store total count for this status
            KanbanTotalCounts[status] = (int)totalCount;
            AllKanbanItems.AddRange(itemsToAdd);
            KanbanLoadedCounts[status] = itemsToAdd.Count;
        }
        else
        {
            // Append new items
            AllKanbanItems.AddRange(itemsToAdd);
            KanbanLoadedCounts[status] += itemsToAdd.Count;
        }
        
            // Return the number of items actually added (new items, not duplicates)
            return itemsToAdd.Count;
        }
        catch
        {
            // If error occurs, ensure loading state is reset
            if (isInitialLoad)
            {
                KanbanLoadingStates[status] = false;
            }
            throw;
        }
    }
    
    private void UpdateDisplayedKanbanItems()
    {
        var result = new List<KanbanItem>();
        foreach (var status in Enum.GetValues<ProjectTaskStatus>())
        {
            var distinctStatusItems = AllKanbanItems
                .GroupBy(item => item.Id)
                .Select(group => group.First()) // Take first item from each group (by Id) - this ensures no duplicates
                .Where(item => item.Status == status) // Then filter by status
                .OrderByDescending(item => item.ProjectTask.CreationTime)
                .ThenByDescending(item => item.Code)
                .ThenByDescending(item => item.Id)
                .ToList();
            
            var currentPage = KanbanPages.GetValueOrDefault(status, 1);
            var pageSize = KanbanPageSizes.GetValueOrDefault(status, KanbanItemsPerColumn);
            var itemsToShow = currentPage * pageSize;
            
            result.AddRange(distinctStatusItems.Take(itemsToShow));
            
            KanbanLoadedCounts[status] = Math.Min(itemsToShow, distinctStatusItems.Count);
        }
        KanbanItems = result;
    }
    
    private async Task LoadMoreKanbanItemsAsync(ProjectTaskStatus status)
    {
        // Set loading state to true
        KanbanLoadingStates[status] = true;
        await InvokeAsync(StateHasChanged);
        
        try
        {
            // Increment page for this status
            var currentPage = KanbanPages.GetValueOrDefault(status, 1);
            KanbanPages[status] = currentPage + 1;
            
            // Load next page
            var itemsAdded = await LoadKanbanItemsForStatusAsync(status, isInitialLoad: false);
            
            // If no items were returned, hide load more button and revert page
            if (itemsAdded == 0)
            {
                KanbanPages[status] = currentPage; // Revert page
            }
            
            // Always update displayed items to reflect current state
            UpdateDisplayedKanbanItems();
            
            // Force kanban component to re-render with new items
            KanbanRenderKey++;
        }
        finally
        {
            // Set loading state to false after load completes
            KanbanLoadingStates[status] = false;
            await InvokeAsync(StateHasChanged);
        }
    }
    
    protected int GetKanbanLoadedCount(ProjectTaskStatus status) => KanbanLoadedCounts.GetValueOrDefault(status, 0);
    protected int GetKanbanTotalCount(ProjectTaskStatus status) => KanbanTotalCounts.GetValueOrDefault(status, 0);
    protected bool IsKanbanStatusLoading(ProjectTaskStatus status) => KanbanLoadingStates.GetValueOrDefault(status, false);
    protected bool HasMoreKanbanItems(ProjectTaskStatus status)
    {
        var loaded = GetKanbanLoadedCount(status);
        var total = GetKanbanTotalCount(status);
        return loaded < total;
    }

    private KanbanItem MapToKanbanItem(ProjectTaskWithNavigationPropertiesDto dto, ProjectTaskStatus? expectedStatus = null)
    {
        // Try to parse status from DTO
        ProjectTaskStatus status;
        if (!Enum.TryParse<ProjectTaskStatus>(dto.ProjectTask.Status, ignoreCase: true, out status))
        {
            // If parse fails, use expectedStatus (from query filter) or default to TODO
            status = expectedStatus ?? ProjectTaskStatus.TODO;
        }
        
        Enum.TryParse<ProjectTaskPriority>(dto.ProjectTask.Priority, ignoreCase: true, out var priority);

        return new KanbanItem
        {
            Id = dto.ProjectTask.Id,
            ProjectName = dto.Project?.Name ?? string.Empty,
            ParentTaskCode = dto.ProjectTask.ParentTaskId,
            ParentTaskTitle = dto.ParentTaskTitle,
            ChildTaskCount = dto.ChildTaskCount,
            Code = dto.ProjectTask.Code,
            Title = dto.ProjectTask.Title,
            Description = dto.ProjectTask.Description,
            DueDate = dto.ProjectTask.DueDate,
            Status = status,
            Priority = priority,
            ProgressPercent = dto.ProjectTask.ProgressPercent,
            Assignees = dto.ProjectTaskAssignments?
                .Select(x => x.User)
                .Where(u => u != null)
                .DistinctBy(u => u.Id)
                .ToList() ?? new List<IdentityUserDto>(),
            DocumentsCount = dto.ProjectTaskDocumentsCount,
            ProjectTask = dto.ProjectTask,
            ProjectTaskWithNavigationProperties = dto
        };
    }

    private async Task GetProjectTasksAsync()
    {
        // Ensure kanban is loaded first
        if (!IsKanbanLoadedOnce)
        {
            await RefreshKanbanAsync();
        }
        
        UpdateProjectTaskListFromKanban();
        await ClearSelection();
    }
    
    private void UpdateProjectTaskListFromKanban()
    {
        // Convert AllKanbanItems to ProjectTaskWithNavigationPropertiesDto for DataGrid
        var allItems = AllKanbanItems
            .Select(item => item.ProjectTaskWithNavigationProperties)
            .OrderByDescending(item => item.ProjectTask.CreationTime)
            .ThenByDescending(item => item.ProjectTask.Code)
            .ThenByDescending(item => item.ProjectTask.Id)
            .ToList();
        
        TotalCount = allItems.Count;
        
        // Apply pagination to displayed list
        var skipCount = (CurrentPage - 1) * PageSize;
        ProjectTaskList = allItems
            .Skip(skipCount)
            .Take(PageSize)
            .ToList();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await RefreshKanbanAsync();
        await GetProjectTasksAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await ProjectTasksAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/project-tasks/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&ParentTaskId={HttpUtility.UrlEncode(Filter.ParentTaskId)}&Code={HttpUtility.UrlEncode(Filter.Code)}&Title={HttpUtility.UrlEncode(Filter.Title)}&Description={HttpUtility.UrlEncode(Filter.Description)}&StartDateMin={Filter.StartDateMin?.ToString("O")}&StartDateMax={Filter.StartDateMax?.ToString("O")}&DueDateMin={Filter.DueDateMin?.ToString("O")}&DueDateMax={Filter.DueDateMax?.ToString("O")}&Priority={HttpUtility.UrlEncode(Filter.Priority)}&Status={HttpUtility.UrlEncode(Filter.Status)}&ProgressPercentMin={Filter.ProgressPercentMin}&ProgressPercentMax={Filter.ProgressPercentMax}&ProjectId={Filter.ProjectId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<ProjectTaskWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        
        // Ensure kanban is loaded
        if (!IsKanbanLoadedOnce)
        {
            await RefreshKanbanAsync();
        }
        
        // Update list from kanban data
        UpdateProjectTaskListFromKanban();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnSelectedCreateTabChanged(string name)
    {
        if (IsNavigatingTab)
        {
            return;
        }

        if ((name == "assignments" || name == "documents") && !IsCreateWizardGeneralSaved)
        {
            SelectedCreateTab = "general";
            return;
        }

        IsNavigatingTab = true;
        try
        {
            await InvokeAsync(StateHasChanged);
            await Task.Delay(100); // Small delay for smooth transition
            SelectedCreateTab = name;
            await InvokeAsync(StateHasChanged);
        }
        finally
        {
            IsNavigatingTab = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private void OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;
    }

    protected virtual async Task OnParentTaskIdChangedAsync()
    {
        Filter.ParentTaskId = SelectedFilterParentTask?.FirstOrDefault()?.Id;
        await SearchAsync();
    }

    protected virtual async Task OnCodeChangedAsync(string? code)
    {
        Filter.Code = code;
        await SearchAsync();
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

    protected virtual async Task OnDueDateMinChangedAsync(DateTime? dueDateMin)
    {
        Filter.DueDateMin = dueDateMin.HasValue ? dueDateMin.Value.Date : dueDateMin;
        await SearchAsync();
    }

    protected virtual async Task OnDueDateMaxChangedAsync(DateTime? dueDateMax)
    {
        Filter.DueDateMax = dueDateMax.HasValue ? dueDateMax.Value.Date.AddDays(1).AddSeconds(-1) : dueDateMax;
        await SearchAsync();
    }

    protected virtual async Task OnPriorityChangedAsync(string? priority)
    {
        Filter.Priority = string.IsNullOrWhiteSpace(priority) ? null : priority;
        await SearchAsync();
    }

    protected virtual async Task OnStatusChangedAsync(string? status)
    {
        Filter.Status = string.IsNullOrWhiteSpace(status) ? null : status;
        await SearchAsync();
    }

    protected virtual async Task OnProgressPercentMinChangedAsync(int? progressPercentMin)
    {
        Filter.ProgressPercentMin = progressPercentMin;
        await DebounceProgressFilterSearchAsync();
    }

    protected virtual async Task OnProgressPercentMaxChangedAsync(int? progressPercentMax)
    {
        Filter.ProgressPercentMax = progressPercentMax;
        await DebounceProgressFilterSearchAsync();
    }

    private async Task DebounceProgressFilterSearchAsync()
    {
        ProgressFilterSearchCts?.Cancel();
        ProgressFilterSearchCts?.Dispose();
        ProgressFilterSearchCts = new CancellationTokenSource();
        var cancellationToken = ProgressFilterSearchCts.Token;

        try
        {
            // Avoid searching on every keystroke while user is still typing numeric filters.
            await Task.Delay(350, cancellationToken);
            await SearchAsync();
        }
        catch (TaskCanceledException)
        {
            // Expected when user keeps typing and a newer search replaces this one.
        }
    }

    protected virtual async Task OnProjectIdChangedAsync(string? projectId)
    {
        Filter.ProjectId = Guid.TryParse(projectId, out var parsedProjectId)
            ? parsedProjectId
            : null;
        ProjectFilterValue = projectId ?? string.Empty;
        await SearchAsync();
    }

    private async Task GetProjectCollectionLookupAsync(string? newValue = null)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task<List<LookupDto<Guid>>> GetProjectCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        ProjectsCollection = (await ProjectTasksAppService.GetProjectLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return ProjectsCollection.ToList();
    }

    private async Task<List<ParentTaskSelectItem>> GetParentTaskCollectionLookupAsync(IReadOnlyList<ParentTaskSelectItem> dbset, string filter, CancellationToken token)
    {
        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
        };

        // UI-first: use Code as parent id (string) because DTO uses ParentTaskId as string.
        var result = await ProjectTasksAppService.GetListAsync(input);
        ParentTasksCollection = result.Items
            // Prevent selecting itself as parent when editing.
            .Where(x => EditingProjectTaskId == Guid.Empty
                || (x.ProjectTask.Id != EditingProjectTaskId
                    && !string.Equals(x.ProjectTask.Code, EditingProjectTask.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(x => new ParentTaskSelectItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}",
            })
            .ToList();

        return ParentTasksCollection.ToList();
    }

    private async Task<List<ParentTaskSelectItem>> GetEditParentTaskCollectionLookupAsync(IReadOnlyList<ParentTaskSelectItem> dbset, string filter, CancellationToken token)
    {
        var currentProjectId = EditingProjectTask.ProjectId;
        if (currentProjectId == Guid.Empty && SelectedEditProjectTaskProject.Any())
        {
            currentProjectId = SelectedEditProjectTaskProject.First().Id;
        }

        if (currentProjectId == Guid.Empty)
        {
            EditParentTasksCollection = new List<ParentTaskSelectItem>();
            return new List<ParentTaskSelectItem>();
        }

        var input = new GetProjectTasksInput
        {
            FilterText = filter,
            MaxResultCount = 20,
            SkipCount = 0,
            ProjectId = currentProjectId
        };

        var result = await ProjectTasksAppService.GetListAsync(input);
        EditParentTasksCollection = result.Items
            .Where(x => EditingProjectTaskId == Guid.Empty
                || (x.ProjectTask.Id != EditingProjectTaskId
                    && !string.Equals(x.ProjectTask.Code, EditingProjectTask.Code, StringComparison.OrdinalIgnoreCase)))
            .Select(x => new ParentTaskSelectItem
            {
                Id = x.ProjectTask.Code,
                DisplayName = $"{x.ProjectTask.Code} - {x.ProjectTask.Title}",
            })
            .ToList();

        return EditParentTasksCollection.ToList();
    }

    protected void OnNewProjectTaskProjectChanged()
    {
        NewProjectTask.ProjectId = SelectedNewProjectTaskProject.FirstOrDefault()?.Id ?? Guid.Empty;
    }

    protected void OnNewProjectTaskParentChanged()
    {
        NewProjectTask.ParentTaskId = SelectedNewProjectTaskParentTask.FirstOrDefault()?.Id;
    }

    protected void OnNewProjectTaskPriorityChanged(ProjectTaskPriority priority)
    {
        NewProjectTaskPriority = priority;
        NewProjectTask.Priority = priority.ToString();
    }

    protected void OnNewProjectTaskStatusChanged(ProjectTaskStatus status)
    {
        NewProjectTaskStatus = status;
        NewProjectTask.Status = status.ToString();
    }

    private Task SelectAllItems()
    {
        AllProjectTasksSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllProjectTasksSelected = false;
        SelectedProjectTasks.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedProjectTaskRowsChanged()
    {
        if (SelectedProjectTasks.Count != PageSize)
        {
            AllProjectTasksSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedProjectTasksAsync()
    {
        var message = AllProjectTasksSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedProjectTasks.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            if (AllProjectTasksSelected)
            {
                await ProjectTasksAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await ProjectTasksAppService.DeleteByIdsAsync(SelectedProjectTasks.Select(x => x.ProjectTask.Id).ToList());
            }

            SelectedProjectTasks.Clear();
            AllProjectTasksSelected = false;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        
            // Reload kanban after deletion
            await RefreshKanbanAsync();
            await GetProjectTasksAsync();
            await InvokeAsync(StateHasChanged);
        }
    }
    
    // PDF Viewer and File Download methods
    private bool IsPdfFileExtension(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
            return false;
        
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension == ".pdf";
    }
    
    private async Task DownloadFileAsync(string? filePath, string fileName)
    {
        if (string.IsNullOrEmpty(filePath))
            return;

        try
        {
            byte[] fileBytes;
            if (IsPdfFileExtension(fileName))
            {
                fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
                {
                    BlobPath = filePath,
                    Action = "download"
                });
            }
            else
            {
                fileBytes = await BlobContainer.GetAllBytesAsync(filePath);
            }
            
            // Create blob URL and download using JavaScript
            var base64 = Convert.ToBase64String(fileBytes);
            var contentType = "application/octet-stream";
            var jsCode = $@"
                (function() {{
                    const blob = new Blob([Uint8Array.from(atob('{base64}'), c => c.charCodeAt(0))], {{ type: '{contentType}' }});
                    const url = window.URL.createObjectURL(blob);
                    const link = document.createElement('a');
                    link.href = url;
                    link.download = '{fileName}';
                    document.body.appendChild(link);
                    link.click();
                    document.body.removeChild(link);
                    window.URL.revokeObjectURL(url);
                }})();
            ";
            
            await JSRuntime.InvokeVoidAsync("eval", jsCode);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error downloading file. FilePath: {filePath}, FileName: {fileName}");
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task<bool> CheckIfDocumentHasPdfFileAsync(Guid documentId)
    {
        try
        {
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = documentId,
                MaxResultCount = 1,
                SkipCount = 0
            });
            
            if (documentFilesResult.Items == null || !documentFilesResult.Items.Any())
            {
                return false;
            }
            
            var documentFile = documentFilesResult.Items.First();
            return IsPdfFileExtension(documentFile.DocumentFile.Name) && !string.IsNullOrEmpty(documentFile.DocumentFile.Path);
        }
        catch
        {
            return false;
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
            
            // Get document files for this document
            var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
            {
                DocumentId = projectTaskDocument.Document.Id,
                MaxResultCount = 1,
                SkipCount = 0
            });
            
            if (documentFilesResult.Items == null || !documentFilesResult.Items.Any())
            {
                await UiMessageService.Warn(L["NoFileAvailable"] ?? L["NoFileAvailable"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            
            var documentFile = documentFilesResult.Items.First();
            
            // Check if file is PDF
            if (!IsPdfFileExtension(documentFile.DocumentFile.Name) || string.IsNullOrEmpty(documentFile.DocumentFile.Path))
            {
                await UiMessageService.Warn(L["FileIsNotPdf"] ?? "File is not a PDF");
                return;
            }

            // Store which modal was open and hide them temporarily
            // Check if modals are actually visible before hiding
            WasCreateModalOpen = false;
            WasEditModalOpen = false;
            
            // Note: Create modal is now in ProjectTaskCreateModal component
            // We don't need to hide it from here as the component manages its own modal
            
            // Check and hide Edit modal if it exists and is visible
            if (EditProjectTaskModal != null)
            {
                try
                {
                    var wasVisible = EditProjectTaskModal.Visible;
                    if (wasVisible)
                    {
                        await EditProjectTaskModal.Hide();
                        WasEditModalOpen = true;
                    }
                }
                catch
                {
                    WasEditModalOpen = false;
                }
            }

            // Get watermarked PDF from API (user + timestamp stamped)
            var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
            {
                BlobPath = documentFile.DocumentFile.Path,
                Action = "view"
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
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error loading PDF for document: {projectTaskDocument?.Document?.Id}");
            await HandleErrorAsync(ex);
        }
    }
    
    private async Task ClosePdfViewerModalAsync()
    {
        if (PdfViewerModal != null)
        {
            await PdfViewerModal.Hide();
        }
        
        // Restore task modals if they were open
        // Note: Create modal is now in ProjectTaskCreateModal component, we don't restore it from here
        WasCreateModalOpen = false;
        
        if (WasEditModalOpen && EditProjectTaskModal != null)
        {
            await EditProjectTaskModal.Show();
            WasEditModalOpen = false;
        }
        
        // Clear PDF data
        PdfFileUrl = null;
        IsPdfFile = false;
    }
    
    private async Task CacheDocumentPdfInfoAsync(IReadOnlyList<ProjectTaskDocumentWithNavigationPropertiesDto> documents)
    {
        foreach (var doc in documents)
        {
            if (doc?.Document?.Id == null || DocumentHasPdfCache.ContainsKey(doc.Document.Id))
            {
                continue;
            }
            
            var hasPdf = await CheckIfDocumentHasPdfFileAsync(doc.Document.Id);
            DocumentHasPdfCache[doc.Document.Id] = hasPdf;
        }
    }
    
    protected bool DocumentHasPdfFile(Guid? documentId)
    {
        if (!documentId.HasValue)
            return false;
        
        return DocumentHasPdfCache.GetValueOrDefault(documentId.Value, false);
    }
    
    private async Task DownloadDocumentFileAsync(ProjectTaskDocumentWithNavigationPropertiesDto projectTaskDocument)
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
            
            if (documentFilesResult.Items == null || !documentFilesResult.Items.Any())
            {
                await UiMessageService.Warn(L["NoFileAvailable"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            
            var documentFile = documentFilesResult.Items.First();
            
            if (string.IsNullOrEmpty(documentFile.DocumentFile.Path))
            {
                await UiMessageService.Warn(L["NoFileAvailable"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }
            
            await DownloadFileAsync(documentFile.DocumentFile.Path, documentFile.DocumentFile.Name);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"Error downloading document file. DocumentId: {projectTaskDocument?.Document?.Id}");
            await HandleErrorAsync(ex);
        }
    }

    private async Task NavigateToProjectTaskDetailAsync(ProjectTaskWithNavigationPropertiesDto projectTask)
    {
        NavigationManager.NavigateTo($"/project-task-detail/{projectTask.ProjectTask.Id}");
        await Task.CompletedTask;
    }
}
