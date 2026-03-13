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
using HC.MasterDatas;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class MasterDatas
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<MasterDataDto> DataGridRef { get; set; }

    private IReadOnlyList<MasterDataDto> MasterDataList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateMasterData { get; set; }

    private bool CanEditMasterData { get; set; }

    private bool CanDeleteMasterData { get; set; }

    private MasterDataCreateDto NewMasterData { get; set; }

    private Validations NewMasterDataValidations { get; set; } = new();
    private MasterDataUpdateDto EditingMasterData { get; set; }

    private Validations EditingMasterDataValidations { get; set; } = new();
    private Guid EditingMasterDataId { get; set; }

    private Modal CreateMasterDataModal { get; set; } = new();
    private Modal EditMasterDataModal { get; set; } = new();
    private GetMasterDatasInput Filter { get; set; }

    private DataGridEntityActionsColumn<MasterDataDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "masterData-create-tab";
    protected string SelectedEditTab = "masterData-edit-tab";

    private List<MasterDataDto> SelectedMasterDatas { get; set; } = new();
    private bool AllMasterDatasSelected { get; set; }

    public MasterDatas()
    {
        NewMasterData = new MasterDataCreateDto();
        EditingMasterData = new MasterDataUpdateDto();
        Filter = new GetMasterDatasInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        MasterDataList = new List<MasterDataDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["MasterDatas"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewMasterData"], async () => {
            await OpenCreateMasterDataModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(MasterDataDto masterData)
    {
        DataGridRef.ToggleDetailRow(masterData, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<MasterDataDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteMasterData;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<MasterDataDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Create);
        CanEditMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Edit);
        CanDeleteMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Delete);
    }

    private async Task GetMasterDatasAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await MasterDatasAppService.GetListAsync(Filter);
        MasterDataList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetMasterDatasAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await MasterDatasAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/master-datas/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Type={HttpUtility.UrlEncode(Filter.Type)}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&SortOrderMin={Filter.SortOrderMin}&SortOrderMax={Filter.SortOrderMax}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<MasterDataDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetMasterDatasAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateMasterDataModalAsync()
    {
        NewMasterData = new MasterDataCreateDto
        {
        };
        SelectedCreateTab = "masterData-create-tab";
        await NewMasterDataValidations.ClearAll();
        await CreateMasterDataModal.Show();
    }

    private async Task CloseCreateMasterDataModalAsync()
    {
        NewMasterData = new MasterDataCreateDto
        {
        };
        await CreateMasterDataModal.Hide();
    }

    private async Task OpenEditMasterDataModalAsync(MasterDataDto input)
    {
        SelectedEditTab = "masterData-edit-tab";
        var masterData = await MasterDatasAppService.GetAsync(input.Id);
        EditingMasterDataId = masterData.Id;
        EditingMasterData = ObjectMapper.Map<MasterDataDto, MasterDataUpdateDto>(masterData);
        await EditingMasterDataValidations.ClearAll();
        await EditMasterDataModal.Show();
    }

    private async Task DeleteMasterDataAsync(MasterDataDto input)
    {
        try
        {
            await MasterDatasAppService.DeleteAsync(input.Id);
            await GetMasterDatasAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteMasterDataWithConfirmationAsync(MasterDataDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteMasterDataAsync(input);
        }
    }

    private async Task CreateMasterDataAsync()
    {
        try
        {
            if (await NewMasterDataValidations.ValidateAll() == false)
            {
                return;
            }

            await MasterDatasAppService.CreateAsync(NewMasterData);
            await GetMasterDatasAsync();
            await CloseCreateMasterDataModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditMasterDataModalAsync()
    {
        await EditMasterDataModal.Hide();
    }

    private async Task UpdateMasterDataAsync()
    {
        try
        {
            if (await EditingMasterDataValidations.ValidateAll() == false)
            {
                return;
            }

            await MasterDatasAppService.UpdateAsync(EditingMasterDataId, EditingMasterData);
            await GetMasterDatasAsync();
            await EditMasterDataModal.Hide();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
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

    protected virtual async Task OnTypeChangedAsync(string? type)
    {
        Filter.Type = type;
        await SearchAsync();
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

    protected virtual async Task OnIsActiveChangedAsync(bool? isActive)
    {
        Filter.IsActive = isActive;
        await SearchAsync();
    }

    private Task SelectAllItems()
    {
        AllMasterDatasSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllMasterDatasSelected = false;
        SelectedMasterDatas.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedMasterDataRowsChanged()
    {
        if (SelectedMasterDatas.Count != PageSize)
        {
            AllMasterDatasSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedMasterDatasAsync()
    {
        var message = AllMasterDatasSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedMasterDatas.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllMasterDatasSelected)
        {
            await MasterDatasAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await MasterDatasAppService.DeleteByIdsAsync(SelectedMasterDatas.Select(x => x.Id).ToList());
        }

        SelectedMasterDatas.Clear();
        AllMasterDatasSelected = false;
        await GetMasterDatasAsync();
    }
}