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
using HC.SurveyLocations;
using HC.Permissions;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp.Http.Client;
using Microsoft.Extensions.Configuration;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;


public partial class SurveyLocations
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<SurveyLocationDto> DataGridRef { get; set; }

    private IReadOnlyList<SurveyLocationDto> SurveyLocationList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateSurveyLocation { get; set; }

    private bool CanEditSurveyLocation { get; set; }

    private bool CanDeleteSurveyLocation { get; set; }

    private SurveyLocationCreateDto NewSurveyLocation { get; set; }

    private Validations NewSurveyLocationValidations { get; set; } = new();
    private SurveyLocationUpdateDto EditingSurveyLocation { get; set; }

    private Validations EditingSurveyLocationValidations { get; set; } = new();
    private Guid EditingSurveyLocationId { get; set; }

    private Modal CreateSurveyLocationModal { get; set; } = new();
    private Modal EditSurveyLocationModal { get; set; } = new();
    private GetSurveyLocationsInput Filter { get; set; }

    private DataGridEntityActionsColumn<SurveyLocationDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "surveyLocation-create-tab";
    protected string SelectedEditTab = "surveyLocation-edit-tab";
    private SurveyLocationDto? SelectedSurveyLocation;

    private List<SurveyLocationDto> SelectedSurveyLocations { get; set; } = new();
    private bool AllSurveyLocationsSelected { get; set; }

    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    [Inject]
    private IConfiguration Configuration { get; set; } = default!;


    private string? _apiBaseUrl;

    public SurveyLocations()
    {
        NewSurveyLocation = new SurveyLocationCreateDto();
        EditingSurveyLocation = new SurveyLocationUpdateDto();
        Filter = new GetSurveyLocationsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        SurveyLocationList = new List<SurveyLocationDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();

        // Get API base URL for image URLs
        _apiBaseUrl = Configuration.GetSection("App:SelfUrl").Value?.EnsureEndsWith('/') ?? string.Empty;
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["SurveyLocations"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewSurveyLocation"], async () => {
            await OpenCreateSurveyLocationModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.SurveyLocationCreate);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(SurveyLocationDto surveyLocation)
    {
        DataGridRef.ToggleDetailRow(surveyLocation, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<SurveyLocationDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteSurveyLocation;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<SurveyLocationDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateSurveyLocation = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SurveyLocationCreate);
        CanEditSurveyLocation = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SurveyLocationEdit);
        CanDeleteSurveyLocation = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SurveyLocationDelete);
    }

    private async Task GetSurveyLocationsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await SurveyLocationsAppService.GetListAsync(Filter);
        SurveyLocationList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetSurveyLocationsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await SurveyLocationsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/survey-locations/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Code={HttpUtility.UrlEncode(Filter.Code)}&Name={HttpUtility.UrlEncode(Filter.Name)}&Description={HttpUtility.UrlEncode(Filter.Description)}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<SurveyLocationDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetSurveyLocationsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateSurveyLocationModalAsync()
    {
        NewSurveyLocation = new SurveyLocationCreateDto
        {
        };
        SelectedCreateTab = "surveyLocation-create-tab";
        await NewSurveyLocationValidations.ClearAll();
        await CreateSurveyLocationModal.Show();
    }

    private async Task CloseCreateSurveyLocationModalAsync()
    {
        NewSurveyLocation = new SurveyLocationCreateDto
        {
        };
        await CreateSurveyLocationModal.Hide();
    }

    private async Task OpenEditSurveyLocationModalAsync(SurveyLocationDto input)
    {
        SelectedEditTab = "surveyLocation-edit-tab";
        var surveyLocation = await SurveyLocationsAppService.GetAsync(input.Id);
        EditingSurveyLocationId = surveyLocation.Id;
        EditingSurveyLocation = ObjectMapper.Map<SurveyLocationDto, SurveyLocationUpdateDto>(surveyLocation);
        await EditingSurveyLocationValidations.ClearAll();
        await EditSurveyLocationModal.Show();
    }

    private async Task DeleteSurveyLocationAsync(SurveyLocationDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SurveyLocationsAppService.DeleteAsync(input.Id);
            await GetSurveyLocationsAsync();
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

    private async Task DeleteSurveyLocationWithConfirmationAsync(SurveyLocationDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteSurveyLocationAsync(input);
        }
    }

    private async Task CreateSurveyLocationAsync()
    {
        try
        {
            if (await NewSurveyLocationValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SurveyLocationsAppService.CreateAsync(NewSurveyLocation);
            await GetSurveyLocationsAsync();
            await CloseCreateSurveyLocationModalAsync();
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

    private async Task CloseEditSurveyLocationModalAsync()
    {
        await EditSurveyLocationModal.Hide();
    }

    private async Task UpdateSurveyLocationAsync()
    {
        try
        {
            if (await EditingSurveyLocationValidations.ValidateAll() == false)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SurveyLocationsAppService.UpdateAsync(EditingSurveyLocationId, EditingSurveyLocation);
            await GetSurveyLocationsAsync();
            await EditSurveyLocationModal.Hide();
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

    private Task SelectAllItems()
    {
        AllSurveyLocationsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllSurveyLocationsSelected = false;
        SelectedSurveyLocations.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedSurveyLocationRowsChanged()
    {
        if (SelectedSurveyLocations.Count != PageSize)
        {
            AllSurveyLocationsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedSurveyLocationsAsync()
    {
        var message = AllSurveyLocationsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedSurveyLocations.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        try{        
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            if (AllSurveyLocationsSelected)
            {
                await SurveyLocationsAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await SurveyLocationsAppService.DeleteByIdsAsync(SelectedSurveyLocations.Select(x => x.Id).ToList());
            }

            SelectedSurveyLocations.Clear();
            AllSurveyLocationsSelected = false;
            await GetSurveyLocationsAsync();            
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

    private async Task CopySurveyLocationToClipboardAsync(SurveyLocationDto input)
    {
        await JSRuntime.InvokeVoidAsync("copyToClipboard", $"{_apiBaseUrl}survey-collections/{input.Id}");
        await UiMessageService.Success(L["SurveyLocationCopiedToClipboard"].Value,
        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        await InvokeAsync(StateHasChanged);
    }
}