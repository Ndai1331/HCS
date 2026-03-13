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
using HC.SurveySessions;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class SurveySessions
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<SurveySessionWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<SurveySessionWithNavigationPropertiesDto> SurveySessionList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateSurveySession { get; set; }

    private bool CanEditSurveySession { get; set; }

    private bool CanDeleteSurveySession { get; set; }

    private SurveySessionCreateDto NewSurveySession { get; set; }

    private Validations NewSurveySessionValidations { get; set; } = new();
    private SurveySessionUpdateDto EditingSurveySession { get; set; }

    private Validations EditingSurveySessionValidations { get; set; } = new();
    private Guid EditingSurveySessionId { get; set; }

    private Modal CreateSurveySessionModal { get; set; } = new();
    private Modal EditSurveySessionModal { get; set; } = new();
    private GetSurveySessionsInput Filter { get; set; }

    private DataGridEntityActionsColumn<SurveySessionWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "surveySession-create-tab";
    protected string SelectedEditTab = "surveySession-edit-tab";

    private IReadOnlyList<LookupDto<Guid>> SurveyLocationsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<SurveySessionWithNavigationPropertiesDto> SelectedSurveySessions { get; set; } = new();
    private bool AllSurveySessionsSelected { get; set; }

    public SurveySessions()
    {
        NewSurveySession = new SurveySessionCreateDto();
        EditingSurveySession = new SurveySessionUpdateDto();
        Filter = new GetSurveySessionsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        SurveySessionList = new List<SurveySessionWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetSurveyLocationCollectionLookupAsync();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["SurveySessions"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewSurveySession"], async () => {
            await OpenCreateSurveySessionModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.SurveySessions.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(SurveySessionWithNavigationPropertiesDto surveySession)
    {
        DataGridRef.ToggleDetailRow(surveySession, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<SurveySessionWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteSurveySession;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<SurveySessionWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateSurveySession = await AuthorizationService.IsGrantedAsync(HCPermissions.SurveySessions.Create);
        CanEditSurveySession = await AuthorizationService.IsGrantedAsync(HCPermissions.SurveySessions.Edit);
        CanDeleteSurveySession = await AuthorizationService.IsGrantedAsync(HCPermissions.SurveySessions.Delete);
    }

    private async Task GetSurveySessionsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await SurveySessionsAppService.GetListAsync(Filter);
        SurveySessionList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetSurveySessionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await SurveySessionsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/survey-sessions/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&FullName={HttpUtility.UrlEncode(Filter.FullName)}&PhoneNumber={HttpUtility.UrlEncode(Filter.PhoneNumber)}&PatientCode={HttpUtility.UrlEncode(Filter.PatientCode)}&SurveyTimeMin={Filter.SurveyTimeMin?.ToString("O")}&SurveyTimeMax={Filter.SurveyTimeMax?.ToString("O")}&DeviceType={HttpUtility.UrlEncode(Filter.DeviceType?.ToString())}&Note={HttpUtility.UrlEncode(Filter.Note)}&SessionDisplay={HttpUtility.UrlEncode(Filter.SessionDisplay)}&SurveyLocationId={Filter.SurveyLocationId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<SurveySessionWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetSurveySessionsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateSurveySessionModalAsync()
    {
        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            SurveyLocationId = SurveyLocationsCollection.Select(i => i.Id).FirstOrDefault(),
        };
        SelectedCreateTab = "surveySession-create-tab";
        await NewSurveySessionValidations.ClearAll();
        await CreateSurveySessionModal.Show();
    }

    private async Task CloseCreateSurveySessionModalAsync()
    {
        NewSurveySession = new SurveySessionCreateDto
        {
            SurveyTime = DateTime.Now,
            SurveyLocationId = SurveyLocationsCollection.Select(i => i.Id).FirstOrDefault(),
        };
        await CreateSurveySessionModal.Hide();
    }

    private async Task OpenEditSurveySessionModalAsync(SurveySessionWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "surveySession-edit-tab";
        var surveySession = await SurveySessionsAppService.GetWithNavigationPropertiesAsync(input.SurveySession.Id);
        EditingSurveySessionId = surveySession.SurveySession.Id;
        EditingSurveySession = ObjectMapper.Map<SurveySessionDto, SurveySessionUpdateDto>(surveySession.SurveySession);
        await EditingSurveySessionValidations.ClearAll();
        await EditSurveySessionModal.Show();
    }

    private async Task DeleteSurveySessionAsync(SurveySessionWithNavigationPropertiesDto input)
    {
        try
        {
            await SurveySessionsAppService.DeleteAsync(input.SurveySession.Id);
            await GetSurveySessionsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteSurveySessionWithConfirmationAsync(SurveySessionWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteSurveySessionAsync(input);
        }
    }

    private async Task CreateSurveySessionAsync()
    {
        try
        {
            if (await NewSurveySessionValidations.ValidateAll() == false)
            {
                return;
            }

            await SurveySessionsAppService.CreateAsync(NewSurveySession);
            await GetSurveySessionsAsync();
            await CloseCreateSurveySessionModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditSurveySessionModalAsync()
    {
        await EditSurveySessionModal.Hide();
    }

    private async Task UpdateSurveySessionAsync()
    {
        try
        {
            if (await EditingSurveySessionValidations.ValidateAll() == false)
            {
                return;
            }

            await SurveySessionsAppService.UpdateAsync(EditingSurveySessionId, EditingSurveySession);
            await GetSurveySessionsAsync();
            await EditSurveySessionModal.Hide();
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

    protected virtual async Task OnFullNameChangedAsync(string? fullName)
    {
        Filter.FullName = fullName;
        await SearchAsync();
    }

    protected virtual async Task OnPhoneNumberChangedAsync(string? phoneNumber)
    {
        Filter.PhoneNumber = phoneNumber;
        await SearchAsync();
    }

    protected virtual async Task OnPatientCodeChangedAsync(string? patientCode)
    {
        Filter.PatientCode = patientCode;
        await SearchAsync();
    }

    protected virtual async Task OnSurveyTimeMinChangedAsync(DateTime? surveyTimeMin)
    {
        Filter.SurveyTimeMin = surveyTimeMin.HasValue ? surveyTimeMin.Value.Date : surveyTimeMin;
        await SearchAsync();
    }

    protected virtual async Task OnSurveyTimeMaxChangedAsync(DateTime? surveyTimeMax)
    {
        Filter.SurveyTimeMax = surveyTimeMax.HasValue ? surveyTimeMax.Value.Date.AddDays(1).AddSeconds(-1) : surveyTimeMax;
        await SearchAsync();
    }

    protected virtual async Task OnDeviceTypeChangedAsync(DeviceType? deviceType)
    {
        Filter.DeviceType = deviceType;
        await SearchAsync();
    }

    protected virtual async Task OnNoteChangedAsync(string? note)
    {
        Filter.Note = note;
        await SearchAsync();
    }

    protected virtual async Task OnSessionDisplayChangedAsync(string? sessionDisplay)
    {
        Filter.SessionDisplay = sessionDisplay;
        await SearchAsync();
    }

    protected virtual async Task OnSurveyLocationIdChangedAsync(Guid? surveyLocationId)
    {
        Filter.SurveyLocationId = surveyLocationId;
        await SearchAsync();
    }

    private async Task GetSurveyLocationCollectionLookupAsync(string? newValue = null)
    {
        SurveyLocationsCollection = (await SurveySessionsAppService.GetSurveyLocationLookupAsync(new LookupRequestDto { Filter = newValue, IsActive = true })).Items;
    }

    private Task SelectAllItems()
    {
        AllSurveySessionsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllSurveySessionsSelected = false;
        SelectedSurveySessions.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedSurveySessionRowsChanged()
    {
        if (SelectedSurveySessions.Count != PageSize)
        {
            AllSurveySessionsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedSurveySessionsAsync()
    {
        var message = AllSurveySessionsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedSurveySessions.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllSurveySessionsSelected)
        {
            await SurveySessionsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await SurveySessionsAppService.DeleteByIdsAsync(SelectedSurveySessions.Select(x => x.SurveySession.Id).ToList());
        }

        SelectedSurveySessions.Clear();
        AllSurveySessionsSelected = false;
        await GetSurveySessionsAsync();
    }

    private void UpdateSessionDisplay()
    {
        if (EditingSurveySessionId != Guid.Empty)
        {
            EditingSurveySession.SessionDisplay =
                $"{EditingSurveySession.FullName}_" +
                $"{EditingSurveySession.PhoneNumber}_" +
                $"{EditingSurveySession.SurveyLocationId}_" +
                $"{EditingSurveySession.SurveyTime:ddMMyyyyHHmm}";
            return;
        } else {
            NewSurveySession.SessionDisplay =
                $"{NewSurveySession.FullName}_" +
                $"{NewSurveySession.PhoneNumber}_" +
                $"{NewSurveySession.SurveyLocationId}_" +
                $"{NewSurveySession.SurveyTime:ddMMyyyyHHmm}";
            return;
        }        
    }
}