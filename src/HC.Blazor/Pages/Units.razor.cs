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
using HC.Units;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class Units
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<UnitDto> DataGridRef { get; set; }

    private IReadOnlyList<UnitDto> UnitList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateUnit { get; set; }

    private bool CanEditUnit { get; set; }

    private bool CanDeleteUnit { get; set; }

    private UnitCreateDto NewUnit { get; set; }

    private Validations NewUnitValidations { get; set; } = new();
    private UnitUpdateDto EditingUnit { get; set; }

    private Validations EditingUnitValidations { get; set; } = new();
    private Guid EditingUnitId { get; set; }

    private Modal CreateUnitModal { get; set; } = new();
    private Modal EditUnitModal { get; set; } = new();
    private GetUnitsInput Filter { get; set; }

    private DataGridEntityActionsColumn<UnitDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "unit-create-tab";
    protected string SelectedEditTab = "unit-edit-tab";

    private List<UnitDto> SelectedUnits { get; set; } = new();
    private bool AllUnitsSelected { get; set; }

    public Units()
    {
        NewUnit = new UnitCreateDto();
        EditingUnit = new UnitUpdateDto();
        Filter = new GetUnitsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        UnitList = new List<UnitDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Units"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewUnit"], async () => {
            await OpenCreateUnitModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.UnitCreate);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(UnitDto unit)
    {
        DataGridRef.ToggleDetailRow(unit, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<UnitDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteUnit;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<UnitDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateUnit = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.UnitCreate);
        CanEditUnit = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.UnitEdit);
        CanDeleteUnit = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.UnitDelete);
    }

    private async Task GetUnitsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await UnitsAppService.GetListAsync(Filter);
        UnitList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetUnitsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await UnitsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/units/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&SortOrderMin={Filter.SortOrderMin}&SortOrderMax={Filter.SortOrderMax}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<UnitDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetUnitsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateUnitModalAsync()
    {
        NewUnit = new UnitCreateDto
        {
        };
        SelectedCreateTab = "unit-create-tab";
        await NewUnitValidations.ClearAll();
        await CreateUnitModal.Show();
    }

    private async Task CloseCreateUnitModalAsync()
    {
        NewUnit = new UnitCreateDto
        {
        };
        await CreateUnitModal.Hide();
    }

    private async Task OpenEditUnitModalAsync(UnitDto input)
    {
        SelectedEditTab = "unit-edit-tab";
        var unit = await UnitsAppService.GetAsync(input.Id);
        EditingUnitId = unit.Id;
        EditingUnit = ObjectMapper.Map<UnitDto, UnitUpdateDto>(unit);
        await EditingUnitValidations.ClearAll();
        await EditUnitModal.Show();
    }

    private async Task DeleteUnitAsync(UnitDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await UnitsAppService.DeleteAsync(input.Id);
            await GetUnitsAsync();
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

    private async Task DeleteUnitWithConfirmationAsync(UnitDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteUnitAsync(input);
        }
    }

    private async Task CreateUnitAsync()
    {
        try
        {
            if (await NewUnitValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await UnitsAppService.CreateAsync(NewUnit);
            await GetUnitsAsync();
            await CloseCreateUnitModalAsync();
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

    private async Task CloseEditUnitModalAsync()
    {
        await EditUnitModal.Hide();
    }

    private async Task UpdateUnitAsync()
    {
        try
        {
            if (await EditingUnitValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await UnitsAppService.UpdateAsync(EditingUnitId, EditingUnit);
            await GetUnitsAsync();
            await EditUnitModal.Hide();
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

    protected virtual async Task OnSortOrderMinChangedAsync(int? sortOrderMin)
    {
        Filter.SortOrderMin = sortOrderMin;
        await SearchAsync();
    }

    protected virtual async Task OnSortOrderMaxChangedAsync(int? sortOrderMax)
    {
        Filter.SortOrderMax = sortOrderMax;
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
        AllUnitsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllUnitsSelected = false;
        SelectedUnits.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedUnitRowsChanged()
    {
        if (SelectedUnits.Count != PageSize)
        {
            AllUnitsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedUnitsAsync()
    {
        var message = AllUnitsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedUnits.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            if (AllUnitsSelected)
            {
                await UnitsAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await UnitsAppService.DeleteByIdsAsync(SelectedUnits.Select(x => x.Id).ToList());
            }

            SelectedUnits.Clear();
            AllUnitsSelected = false;
            await GetUnitsAsync();
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