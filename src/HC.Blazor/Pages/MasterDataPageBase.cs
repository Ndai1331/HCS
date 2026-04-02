using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.Web;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Volo.Abp.BlazoriseUI;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.MasterDatas;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Volo.Abp;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.AspNetCore.Components.Web;
using Volo.Abp.Http.Client;
using HC.Localization;

namespace HC.Blazor.Pages;

public abstract class MasterDataPageBase : HCComponentBase
{
    protected abstract string DefaultType { get; }
    protected abstract string PageTitle { get; }
    protected abstract string PageRoute { get; }

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<MasterDataDto> DataGridRef { get; set; }

    protected IReadOnlyList<MasterDataDto> MasterDataList { get; set; }

    protected int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    protected int TotalCount { get; set; }

    protected bool CanCreateMasterData { get; set; }

    protected bool CanEditMasterData { get; set; }

    protected bool CanDeleteMasterData { get; set; }

    protected MasterDataCreateDto NewMasterData { get; set; }

    protected Validations NewMasterDataValidations { get; set; } = new();
    protected MasterDataUpdateDto EditingMasterData { get; set; }

    protected Validations EditingMasterDataValidations { get; set; } = new();
    private Guid EditingMasterDataId { get; set; }

    protected Modal CreateMasterDataModal { get; set; } = new();
    protected Modal EditMasterDataModal { get; set; } = new();
    protected GetMasterDatasInput Filter { get; set; }

    protected DataGridEntityActionsColumn<MasterDataDto> EntityActionsColumn { get; set; } = new();

    protected List<MasterDataDto> SelectedMasterDatas { get; set; } = new();
    protected bool AllMasterDatasSelected { get; set; }

    [Inject] protected IMasterDatasAppService MasterDatasAppService { get; set; }
    [Inject] protected IUiMessageService UiMessageService { get; set; }
    [Inject] protected AbpBlazorMessageLocalizerHelper<HCResource> LH { get; set; }
    [Inject] protected IRemoteServiceConfigurationProvider RemoteServiceConfigurationProvider { get; set; }
    [Inject] protected NavigationManager NavigationManager { get; set; }

    public MasterDataPageBase()
    {
        NewMasterData = new MasterDataCreateDto();
        EditingMasterData = new MasterDataUpdateDto();
        Filter = new GetMasterDatasInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting,
            Type = DefaultType // Set default type
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
            // List loads only via DataGrid ReadData -> OnDataGridReadAsync (avoid duplicate GET on first paint)
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(PageTitle));
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

    protected bool RowSelectableHandler(RowSelectableEventArgs<MasterDataDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteMasterData;

    private async Task SetPermissionsAsync()
    {
        CanCreateMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Create);
        CanEditMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Edit);
        CanDeleteMasterData = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.Delete);
    }

    protected async Task GetMasterDatasAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        Filter.Type = DefaultType; // Always filter by default type
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
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/master-datas/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Type={HttpUtility.UrlEncode(DefaultType)}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&SortOrderMin={Filter.SortOrderMin}&SortOrderMax={Filter.SortOrderMax}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    protected async Task OnDataGridReadAsync(DataGridReadDataEventArgs<MasterDataDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetMasterDatasAsync();
        await InvokeAsync(StateHasChanged);
    }

    protected async Task OpenCreateMasterDataModalAsync()
    {
        NewMasterData = new MasterDataCreateDto
        {
            Type = DefaultType // Set default type when creating
        };
        await NewMasterDataValidations.ClearAll();
        await CreateMasterDataModal.Show();
    }

    protected async Task CloseCreateMasterDataModalAsync()
    {
        NewMasterData = new MasterDataCreateDto
        {
            Type = DefaultType
        };
        await CreateMasterDataModal.Hide();
    }

    protected async Task OpenEditMasterDataModalAsync(MasterDataDto input)
    {
        var masterData = await MasterDatasAppService.GetAsync(input.Id);
        EditingMasterDataId = masterData.Id;
        EditingMasterData = ObjectMapper.Map<MasterDataDto, MasterDataUpdateDto>(masterData);
        await EditingMasterDataValidations.ClearAll();
        await EditMasterDataModal.Show();
    }

    protected async Task DeleteMasterDataAsync(MasterDataDto input)
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

    protected async Task DeleteMasterDataWithConfirmationAsync(MasterDataDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteMasterDataAsync(input);
        }
    }

    protected async Task CreateMasterDataAsync()
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

    protected async Task CloseEditMasterDataModalAsync()
    {
        await EditMasterDataModal.Hide();
    }

    protected async Task UpdateMasterDataAsync()
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

    // Use string for Select display - "All" (empty) works correctly (same pattern as WorkflowDefinitions)
    protected string IsActiveFilterValue { get; set; } = string.Empty;

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

    protected Task SelectAllItems()
    {
        AllMasterDatasSelected = true;
        return Task.CompletedTask;
    }

    protected Task ClearSelection()
    {
        AllMasterDatasSelected = false;
        SelectedMasterDatas.Clear();
        return Task.CompletedTask;
    }

    protected Task SelectedMasterDataRowsChanged()
    {
        if (SelectedMasterDatas.Count != PageSize)
        {
            AllMasterDatasSelected = false;
        }

        return Task.CompletedTask;
    }

    protected async Task DeleteSelectedMasterDatasAsync()
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

