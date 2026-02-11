using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
using HC.Workflows;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages.Workflows;

public partial class Workflows
{
    [Inject] private IMemoryCache __MemoryCache { get; set; } = default!;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<WorkflowWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<WorkflowWithNavigationPropertiesDto> WorkflowList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateWorkflow { get; set; }

    private bool CanEditWorkflow { get; set; }

    private bool CanDeleteWorkflow { get; set; }

    private WorkflowCreateDto NewWorkflow { get; set; }

    private Validations NewWorkflowValidations { get; set; } = new();
    private WorkflowUpdateDto EditingWorkflow { get; set; }

    private Validations EditingWorkflowValidations { get; set; } = new();
    private Guid EditingWorkflowId { get; set; }

    private Modal CreateWorkflowModal { get; set; } = new();
    private Modal EditWorkflowModal { get; set; } = new();
    private GetWorkflowsInput Filter { get; set; }

    private DataGridEntityActionsColumn<WorkflowWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "workflow-create-tab";
    protected string SelectedEditTab = "workflow-edit-tab";
    private WorkflowWithNavigationPropertiesDto? SelectedWorkflow;

    private IReadOnlyList<LookupDto<Guid>> WorkflowDefinitionsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedFilterWorkflowDefinition { get; set; } = new();
    private List<LookupDto<Guid>> SelectedNewWorkflowDefinition { get; set; } = new();
    private List<LookupDto<Guid>> SelectedEditWorkflowDefinition { get; set; } = new();
    private List<WorkflowWithNavigationPropertiesDto> SelectedWorkflows { get; set; } = new();
    private bool AllWorkflowsSelected { get; set; }

    public Workflows()
    {
        NewWorkflow = new WorkflowCreateDto();
        EditingWorkflow = new WorkflowUpdateDto();
        Filter = new GetWorkflowsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        WorkflowList = new List<WorkflowWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetWorkflowDefinitionCollectionLookupAsync();
        
        // Initialize selected filter workflow definition if filter has value
        if (Filter.WorkflowDefinitionId.HasValue)
        {
            var workflowDef = WorkflowDefinitionsCollection.FirstOrDefault(x => x.Id == Filter.WorkflowDefinitionId.Value);
            if (workflowDef != null)
            {
                SelectedFilterWorkflowDefinition = new List<LookupDto<Guid>> { workflowDef };
            }
        }
        
        // Load workflows on initialization
        await GetWorkflowsAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Workflows"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewWorkflow"], async () => {
            // await OpenCreateWorkflowModalAsync();
            NavigationManager?.NavigateTo($"/workflow-detail");
        }, IconName.Add, requiredPolicyName: HCPermissions.Workflows.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(WorkflowWithNavigationPropertiesDto workflow)
    {
        DataGridRef.ToggleDetailRow(workflow, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<WorkflowWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteWorkflow;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<WorkflowWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateWorkflow = await AuthorizationService.IsGrantedAsync(HCPermissions.Workflows.Create);
        CanEditWorkflow = await AuthorizationService.IsGrantedAsync(HCPermissions.Workflows.Edit);
        CanDeleteWorkflow = await AuthorizationService.IsGrantedAsync(HCPermissions.Workflows.Delete);
    }

    private async Task GetWorkflowsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await WorkflowsAppService.GetListAsync(Filter);
        WorkflowList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetWorkflowsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await WorkflowsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/workflows/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&Description={HttpUtility.UrlEncode(Filter.Description)}&IsActive={Filter.IsActive}&WorkflowDefinitionId={Filter.WorkflowDefinitionId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<WorkflowWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetWorkflowsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateWorkflowModalAsync()
    {
        NewWorkflow = new WorkflowCreateDto();
        SelectedNewWorkflowDefinition = new List<LookupDto<Guid>>();
        SelectedCreateTab = "workflow-create-tab";
        await NewWorkflowValidations.ClearAll();
        await CreateWorkflowModal.Show();
    }

    private async Task CloseCreateWorkflowModalAsync()
    {
        NewWorkflow = new WorkflowCreateDto();
        SelectedNewWorkflowDefinition = new List<LookupDto<Guid>>();
        await CreateWorkflowModal.Hide();
    }

    private async Task OpenEditWorkflowModalAsync(WorkflowWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "workflow-edit-tab";
        var workflow = await WorkflowsAppService.GetWithNavigationPropertiesAsync(input.Workflow.Id);
        EditingWorkflowId = workflow.Workflow.Id;
        EditingWorkflow = ObjectMapper.Map<WorkflowDto, WorkflowUpdateDto>(workflow.Workflow);
        
        // Set selected workflow definition for Select2
        if (EditingWorkflow.WorkflowDefinitionId != default)
        {
            await GetWorkflowDefinitionCollectionLookupAsync();
            var workflowDef = WorkflowDefinitionsCollection.FirstOrDefault(x => x.Id == EditingWorkflow.WorkflowDefinitionId);
            if (workflowDef != null)
            {
                SelectedEditWorkflowDefinition = new List<LookupDto<Guid>> { workflowDef };
            }
            else
            {
                SelectedEditWorkflowDefinition = new List<LookupDto<Guid>>();
            }
        }
        else
        {
            SelectedEditWorkflowDefinition = new List<LookupDto<Guid>>();
        }
        
        await EditingWorkflowValidations.ClearAll();
        await EditWorkflowModal.Show();
    }

    private async Task DeleteWorkflowAsync(WorkflowWithNavigationPropertiesDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await WorkflowsAppService.DeleteAsync(input.Workflow.Id);
            await GetWorkflowsAsync();
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

    private async Task DeleteWorkflowWithConfirmationAsync(WorkflowWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteWorkflowAsync(input);
        }
    }

    private async Task CreateWorkflowAsync()
    {
        try
        {
            if (await NewWorkflowValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            // Set WorkflowDefinitionId from Select2
            NewWorkflow.WorkflowDefinitionId = SelectedNewWorkflowDefinition?.FirstOrDefault()?.Id ?? default;
            
            if (NewWorkflow.WorkflowDefinitionId == default)
            {
                await UiMessageService.Warn(L["The {0} field is required.", L["WorkflowDefinition"]],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            var created = await WorkflowsAppService.CreateAsync(NewWorkflow);
            await CloseCreateWorkflowModalAsync();
            // Redirect to detail page for new workflow
            NavigationManager.NavigateTo($"/workflow-detail/{created.Id}");
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

    private async Task CloseEditWorkflowModalAsync()
    {
        await EditWorkflowModal.Hide();
    }

    private async Task UpdateWorkflowAsync()
    {
        try
        {
            if (await EditingWorkflowValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            // Set WorkflowDefinitionId from Select2
            EditingWorkflow.WorkflowDefinitionId = SelectedEditWorkflowDefinition?.FirstOrDefault()?.Id ?? default;
            
            if (EditingWorkflow.WorkflowDefinitionId == default)
            {
                await UiMessageService.Warn(L["The {0} field is required.", L["WorkflowDefinition"]],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await WorkflowsAppService.UpdateAsync(EditingWorkflowId, EditingWorkflow);
            await GetWorkflowsAsync();
            await EditWorkflowModal.Hide();
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

    private void OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;
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

    protected virtual async Task OnIsActiveChangedAsync(bool? isActive)
    {
        Filter.IsActive = isActive;
        await SearchAsync();
    }

    protected virtual async Task OnWorkflowDefinitionIdChangedAsync(Guid? workflowDefinitionId)
    {
        Filter.WorkflowDefinitionId = workflowDefinitionId;
        await SearchAsync();
    }

    protected virtual async Task OnFilterWorkflowDefinitionChanged()
    {
        Filter.WorkflowDefinitionId = SelectedFilterWorkflowDefinition?.FirstOrDefault()?.Id;
        await SearchAsync();
    }

    private async Task GetWorkflowDefinitionCollectionLookupAsync(string? newValue = null)
    {
        WorkflowDefinitionsCollection = (await WorkflowsAppService.GetWorkflowDefinitionLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task<List<LookupDto<Guid>>> GetWorkflowDefinitionCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await WorkflowsAppService.GetWorkflowDefinitionLookupAsync(new LookupRequestDto { Filter = filter });
        WorkflowDefinitionsCollection = result.Items;
        return result.Items.ToList();
    }

    private void OnNewWorkflowDefinitionChanged()
    {
        NewWorkflow.WorkflowDefinitionId = SelectedNewWorkflowDefinition?.FirstOrDefault()?.Id ?? default;
        InvokeAsync(StateHasChanged);
    }

    private void OnEditWorkflowDefinitionChanged()
    {
        EditingWorkflow.WorkflowDefinitionId = SelectedEditWorkflowDefinition?.FirstOrDefault()?.Id ?? default;
        InvokeAsync(StateHasChanged);
    }

    private Task SelectAllItems()
    {
        AllWorkflowsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllWorkflowsSelected = false;
        SelectedWorkflows.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedWorkflowRowsChanged()
    {
        if (SelectedWorkflows.Count != PageSize)
        {
            AllWorkflowsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedWorkflowsAsync()
    {
        var message = AllWorkflowsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedWorkflows.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllWorkflowsSelected)
        {
            await WorkflowsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await WorkflowsAppService.DeleteByIdsAsync(SelectedWorkflows.Select(x => x.Workflow.Id).ToList());
        }

        SelectedWorkflows.Clear();
        AllWorkflowsSelected = false;
        await GetWorkflowsAsync();
    }

    private Task NavigateToDetailAsync(Guid workflowId)
    {
        NavigationManager.NavigateTo($"/workflow-detail/{workflowId}");
        return Task.CompletedTask;
    }
}