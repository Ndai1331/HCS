using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Web;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.WorkflowDefinitions;
using HC.Permissions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages.Workflows;

public partial class WorkflowDefinitions
{
    [Inject] private ILogger<WorkflowDefinitions> _logger { get; set; }
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<WorkflowDefinitionDto> DataGridRef { get; set; }

    private IReadOnlyList<WorkflowDefinitionDto> WorkflowDefinitionList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateWorkflowDefinition { get; set; }

    private bool CanEditWorkflowDefinition { get; set; }

    private bool CanDeleteWorkflowDefinition { get; set; }

    private WorkflowDefinitionCreateDto NewWorkflowDefinition { get; set; }

    private Validations NewWorkflowDefinitionValidations { get; set; } = new();
    private WorkflowDefinitionUpdateDto EditingWorkflowDefinition { get; set; }

    private Validations EditingWorkflowDefinitionValidations { get; set; } = new();
    private Guid EditingWorkflowDefinitionId { get; set; }

    private Modal CreateWorkflowDefinitionModal { get; set; } = new();
    private Modal EditWorkflowDefinitionModal { get; set; } = new();
    private GetWorkflowDefinitionsInput Filter { get; set; }

    private DataGridEntityActionsColumn<WorkflowDefinitionDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "workflowDefinition-create-tab";
    protected string SelectedEditTab = "workflowDefinition-edit-tab";
    private WorkflowDefinitionDto? SelectedWorkflowDefinition;

    private List<WorkflowDefinitionDto> SelectedWorkflowDefinitions { get; set; } = new();
    private bool AllWorkflowDefinitionsSelected { get; set; }

    public WorkflowDefinitions()
    {
        NewWorkflowDefinition = new WorkflowDefinitionCreateDto();
        EditingWorkflowDefinition = new WorkflowDefinitionUpdateDto();
        Filter = new GetWorkflowDefinitionsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        WorkflowDefinitionList = new List<WorkflowDefinitionDto>();
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
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["WorkflowDefinitions"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewWorkflowDefinition"], async () => {
            await OpenCreateWorkflowDefinitionModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.WorkflowDefinitions.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(WorkflowDefinitionDto workflowDefinition)
    {
        DataGridRef.ToggleDetailRow(workflowDefinition, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<WorkflowDefinitionDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteWorkflowDefinition;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<WorkflowDefinitionDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateWorkflowDefinition = await AuthorizationService.IsGrantedAsync(HCPermissions.WorkflowDefinitions.Create);
        CanEditWorkflowDefinition = await AuthorizationService.IsGrantedAsync(HCPermissions.WorkflowDefinitions.Edit);
        CanDeleteWorkflowDefinition = await AuthorizationService.IsGrantedAsync(HCPermissions.WorkflowDefinitions.Delete);
    }

    private async Task GetWorkflowDefinitionsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await WorkflowDefinitionsAppService.GetListAsync(Filter);
        WorkflowDefinitionList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetWorkflowDefinitionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await WorkflowDefinitionsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/workflow-definitions/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&Description={HttpUtility.UrlEncode(Filter.Description)}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<WorkflowDefinitionDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetWorkflowDefinitionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateWorkflowDefinitionModalAsync()
    {
        NewWorkflowDefinition = new WorkflowDefinitionCreateDto
        {
        };
        SelectedCreateTab = "workflowDefinition-create-tab";
        await NewWorkflowDefinitionValidations.ClearAll();
        await CreateWorkflowDefinitionModal.Show();
    }

    private async Task CloseCreateWorkflowDefinitionModalAsync()
    {
        NewWorkflowDefinition = new WorkflowDefinitionCreateDto
        {
        };
        await CreateWorkflowDefinitionModal.Hide();
    }

    private async Task OpenEditWorkflowDefinitionModalAsync(WorkflowDefinitionDto input)
    {
        SelectedEditTab = "workflowDefinition-edit-tab";
        var workflowDefinition = await WorkflowDefinitionsAppService.GetAsync(input.Id);
        EditingWorkflowDefinitionId = workflowDefinition.Id;
        EditingWorkflowDefinition = ObjectMapper.Map<WorkflowDefinitionDto, WorkflowDefinitionUpdateDto>(workflowDefinition);
        await EditingWorkflowDefinitionValidations.ClearAll();
        await EditWorkflowDefinitionModal.Show();
    }

    private async Task DeleteWorkflowDefinitionAsync(WorkflowDefinitionDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await WorkflowDefinitionsAppService.DeleteAsync(input.Id);
            await GetWorkflowDefinitionsAsync();
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

    private async Task DeleteWorkflowDefinitionWithConfirmationAsync(WorkflowDefinitionDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteWorkflowDefinitionAsync(input);
        }
    }

    private async Task CreateWorkflowDefinitionAsync()
    {
        try
        {
            if (await NewWorkflowDefinitionValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await WorkflowDefinitionsAppService.CreateAsync(NewWorkflowDefinition);
            await GetWorkflowDefinitionsAsync();
            await CloseCreateWorkflowDefinitionModalAsync();
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

    private async Task CloseEditWorkflowDefinitionModalAsync()
    {
        await EditWorkflowDefinitionModal.Hide();
    }

    private async Task UpdateWorkflowDefinitionAsync()
    {
        try
        {
            if (await EditingWorkflowDefinitionValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await WorkflowDefinitionsAppService.UpdateAsync(EditingWorkflowDefinitionId, EditingWorkflowDefinition);
            await GetWorkflowDefinitionsAsync();
            await EditWorkflowDefinitionModal.Hide();
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
    private string IsActiveFilterValue { get; set; } = string.Empty;
    protected virtual async Task OnIsActiveChangedAsync(string? isActive)
    {
        Filter.IsActive = isActive switch
        {
            "True" or "true" => true,
            "False" or "false" => false,
            _ => null
        };
        IsActiveFilterValue = isActive ?? string.Empty;
        await SearchAsync();
    }

    private Task SelectAllItems()
    {
        AllWorkflowDefinitionsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllWorkflowDefinitionsSelected = false;
        SelectedWorkflowDefinitions.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedWorkflowDefinitionRowsChanged()
    {
        if (SelectedWorkflowDefinitions.Count != PageSize)
        {
            AllWorkflowDefinitionsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedWorkflowDefinitionsAsync()
    {
        var message = AllWorkflowDefinitionsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedWorkflowDefinitions.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllWorkflowDefinitionsSelected)
        {
            await WorkflowDefinitionsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await WorkflowDefinitionsAppService.DeleteByIdsAsync(SelectedWorkflowDefinitions.Select(x => x.Id).ToList());
        }

        SelectedWorkflowDefinitions.Clear();
        AllWorkflowDefinitionsSelected = false;
        await GetWorkflowDefinitionsAsync();
    }
}