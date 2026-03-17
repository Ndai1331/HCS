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
using HC.Positions;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class Positions
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<PositionDto> DataGridRef { get; set; }

    private IReadOnlyList<PositionDto> PositionList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreatePosition { get; set; }

    private bool CanEditPosition { get; set; }

    private bool CanDeletePosition { get; set; }

    private PositionCreateDto NewPosition { get; set; }

    private Validations NewPositionValidations { get; set; } = new();
    private PositionUpdateDto EditingPosition { get; set; }

    private Validations EditingPositionValidations { get; set; } = new();
    private Guid EditingPositionId { get; set; }

    private Modal CreatePositionModal { get; set; } = new();
    private Modal EditPositionModal { get; set; } = new();
    private GetPositionsInput Filter { get; set; }

    private DataGridEntityActionsColumn<PositionDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "position-create-tab";
    protected string SelectedEditTab = "position-edit-tab";

    private List<PositionDto> SelectedPositions { get; set; } = new();
    private bool AllPositionsSelected { get; set; }

    public Positions()
    {
        NewPosition = new PositionCreateDto();
        EditingPosition = new PositionUpdateDto();
        Filter = new GetPositionsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        PositionList = new List<PositionDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Positions"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewPosition"], async () => {
            await OpenCreatePositionModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.PositionCreate);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(PositionDto position)
    {
        DataGridRef.ToggleDetailRow(position, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<PositionDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeletePosition;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<PositionDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreatePosition = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.PositionCreate);
        CanEditPosition = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.PositionEdit);
        CanDeletePosition = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.PositionDelete);
    }

    private async Task GetPositionsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await PositionsAppService.GetListAsync(Filter);
        PositionList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetPositionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await PositionsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/positions/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&SignOrderMin={Filter.SignOrderMin}&SignOrderMax={Filter.SignOrderMax}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<PositionDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetPositionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreatePositionModalAsync()
    {
        NewPosition = new PositionCreateDto
        {
        };
        SelectedCreateTab = "position-create-tab";
        await NewPositionValidations.ClearAll();
        await CreatePositionModal.Show();
    }

    private async Task CloseCreatePositionModalAsync()
    {
        NewPosition = new PositionCreateDto
        {
        };
        await CreatePositionModal.Hide();
    }

    private async Task OpenEditPositionModalAsync(PositionDto input)
    {
        SelectedEditTab = "position-edit-tab";
        var position = await PositionsAppService.GetAsync(input.Id);
        EditingPositionId = position.Id;
        EditingPosition = ObjectMapper.Map<PositionDto, PositionUpdateDto>(position);
        await EditingPositionValidations.ClearAll();
        await EditPositionModal.Show();
    }

    private async Task DeletePositionAsync(PositionDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await PositionsAppService.DeleteAsync(input.Id);
            await GetPositionsAsync();
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

    private async Task DeletePositionWithConfirmationAsync(PositionDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeletePositionAsync(input);
        }
    }

    private async Task CreatePositionAsync()
    {
        try
        {
            if (await NewPositionValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await PositionsAppService.CreateAsync(NewPosition);
            await GetPositionsAsync();
            await CloseCreatePositionModalAsync();
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

    private async Task CloseEditPositionModalAsync()
    {
        await EditPositionModal.Hide();
    }

    private async Task UpdatePositionAsync()
    {
        try
        {
            if (await EditingPositionValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await PositionsAppService.UpdateAsync(EditingPositionId, EditingPosition);
            await GetPositionsAsync();
            await EditPositionModal.Hide();
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

    protected virtual async Task OnSignOrderMinChangedAsync(int? signOrderMin)
    {
        Filter.SignOrderMin = signOrderMin;
        await SearchAsync();
    }

    protected virtual async Task OnSignOrderMaxChangedAsync(int? signOrderMax)
    {
        Filter.SignOrderMax = signOrderMax;
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
        AllPositionsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllPositionsSelected = false;
        SelectedPositions.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedPositionRowsChanged()
    {
        if (SelectedPositions.Count != PageSize)
        {
            AllPositionsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedPositionsAsync()
    {
        var message = AllPositionsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedPositions.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        try{
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            if (AllPositionsSelected)
            {
                await PositionsAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await PositionsAppService.DeleteByIdsAsync(SelectedPositions.Select(x => x.Id).ToList());
            }

            SelectedPositions.Clear();
            AllPositionsSelected = false;
            await GetPositionsAsync();
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