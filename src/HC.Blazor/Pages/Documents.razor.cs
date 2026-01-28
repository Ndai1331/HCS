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
using HC.Documents;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages;

public partial class Documents
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentWithNavigationPropertiesDto> DataGridRef { get; set; } = new();

    private IReadOnlyList<DocumentWithNavigationPropertiesDto> DocumentList { get; set; } = new List<DocumentWithNavigationPropertiesDto>();

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateDocument { get; set; }

    private bool CanEditDocument { get; set; }

    private bool CanDeleteDocument { get; set; }

    private GetDocumentsInput Filter { get; set; } = new GetDocumentsInput();

    private DataGridEntityActionsColumn<DocumentWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    private IReadOnlyList<LookupDto<Guid>> MasterDatasCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UnitsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<DocumentWithNavigationPropertiesDto> SelectedDocuments { get; set; } = new();
    private bool AllDocumentsSelected { get; set; } = false;


    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Logger.LogInformation("Documents OnAfterRenderAsync start");

        if (firstRender)
        {
            Logger.LogInformation("Documents OnAfterRenderAsync firstRender");
            await SetPermissionsAsync();
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            // await GetDocumentsAsync();
            await InvokeAsync(StateHasChanged);
        }
        Logger.LogInformation("Documents OnAfterRenderAsync end");
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Documents"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);

        if (CanCreateDocument)
        {
            Toolbar.AddButton(L["NewDocument"], async () => {
                NavigationManager.NavigateTo("/document-detail");
            }, IconName.Add, requiredPolicyName: HCPermissions.Documents.Create);
        }

        return ValueTask.CompletedTask;
    }
    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocument;

    private async Task SetPermissionsAsync()
    {
        CanCreateDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Create);
        CanEditDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Edit);
        CanDeleteDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Delete);
    }

    private async Task GetDocumentsAsync()
    {
        Logger.LogInformation("GetDocumentsAsync start");
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await DocumentsAppService.GetListAsync(Filter);
        DocumentList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
        Logger.LogInformation("GetDocumentsAsync end");
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await DocumentsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/documents/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&No={HttpUtility.UrlEncode(Filter.No)}&Title={HttpUtility.UrlEncode(Filter.Title)}&CurrentStatus={HttpUtility.UrlEncode(Filter.CurrentStatus)}&CompletedTimeMin={Filter.CompletedTimeMin?.ToString("O")}&CompletedTimeMax={Filter.CompletedTimeMax?.ToString("O")}&StorageNumber={HttpUtility.UrlEncode(Filter.StorageNumber)}&IncommingDateMin={Filter.IncommingDateMin?.ToString("O")}&IncommingDateMax={Filter.IncommingDateMax?.ToString("O")}&FieldId={Filter.FieldId}&UnitId={Filter.UnitId}&WorkflowId={Filter.WorkflowId}&StatusId={Filter.StatusId}&TypeId={Filter.TypeId}&UrgencyLevelId={Filter.UrgencyLevelId}&SecrecyLevelId={Filter.SecrecyLevelId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DeleteDocumentAsync(DocumentWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentsAppService.DeleteAsync(input.Document.Id);
            await GetDocumentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }


    protected virtual async Task OnNoChangedAsync(string? no)
    {
        Filter.No = no;
        await SearchAsync();
    }

    protected virtual async Task OnTitleChangedAsync(string? title)
    {
        Filter.Title = title;
        await SearchAsync();
    }

    protected virtual async Task OnCurrentStatusChangedAsync(string? currentStatus)
    {
        Filter.CurrentStatus = currentStatus;
        await SearchAsync();
    }

    protected virtual async Task OnCompletedTimeMinChangedAsync(DateTime? completedTimeMin)
    {
        Filter.CompletedTimeMin = completedTimeMin.HasValue ? completedTimeMin.Value.Date : completedTimeMin;
        await SearchAsync();
    }

    protected virtual async Task OnCompletedTimeMaxChangedAsync(DateTime? completedTimeMax)
    {
        Filter.CompletedTimeMax = completedTimeMax.HasValue ? completedTimeMax.Value.Date.AddDays(1).AddSeconds(-1) : completedTimeMax;
        await SearchAsync();
    }

    protected virtual async Task OnStorageNumberChangedAsync(string? storageNumber)
    {
        Filter.StorageNumber = storageNumber;
        await SearchAsync();
    }

    protected virtual async Task OnIncommingDateMinChangedAsync(DateTime? incommingDateMin)
    {
        Filter.IncommingDateMin = incommingDateMin.HasValue ? incommingDateMin.Value.Date : incommingDateMin;
        await SearchAsync();
    }

    protected virtual async Task OnIncommingDateMaxChangedAsync(DateTime? incommingDateMax)
    {
        Filter.IncommingDateMax = incommingDateMax.HasValue ? incommingDateMax.Value.Date.AddDays(1).AddSeconds(-1) : incommingDateMax;
        await SearchAsync();
    }

    protected virtual async Task OnFieldIdChangedAsync(Guid? fieldId)
    {
        Filter.FieldId = fieldId;
        await SearchAsync();
    }

    protected virtual async Task OnUnitIdChangedAsync(Guid? unitId)
    {
        Filter.UnitId = unitId;
        await SearchAsync();
    }

    protected virtual async Task OnWorkflowIdChangedAsync(Guid? workflowId)
    {
        Filter.WorkflowId = workflowId;
        await SearchAsync();
    }

    protected virtual async Task OnStatusIdChangedAsync(Guid? statusId)
    {
        Filter.StatusId = statusId;
        await SearchAsync();
    }

    protected virtual async Task OnTypeIdChangedAsync(Guid? typeId)
    {
        Filter.TypeId = typeId;
        await SearchAsync();
    }

    protected virtual async Task OnUrgencyLevelIdChangedAsync(Guid? urgencyLevelId)
    {
        Filter.UrgencyLevelId = urgencyLevelId;
        await SearchAsync();
    }

    protected virtual async Task OnSecrecyLevelIdChangedAsync(Guid? secrecyLevelId)
    {
        Filter.SecrecyLevelId = secrecyLevelId;
        await SearchAsync();
    }

    private async Task GetMasterDataCollectionLookupAsync(string? newValue = null)
    {
        MasterDatasCollection = (await DocumentsAppService.GetMasterDataLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetUnitCollectionLookupAsync(string? newValue = null)
    {
        UnitsCollection = (await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetWorkflowCollectionLookupAsync(string? newValue = null)
    {
        WorkflowsCollection = (await DocumentsAppService.GetWorkflowLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllDocumentsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllDocumentsSelected = false;
        SelectedDocuments.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedDocumentRowsChanged()
    {
        if (SelectedDocuments.Count != PageSize)
        {
            AllDocumentsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedDocumentsAsync()
    {
        var message = AllDocumentsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedDocuments.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllDocumentsSelected)
        {
            await DocumentsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await DocumentsAppService.DeleteByIdsAsync(SelectedDocuments.Select(x => x.Document.Id).ToList());
        }

        SelectedDocuments.Clear();
        AllDocumentsSelected = false;
        await GetDocumentsAsync();
    }

    private async Task OpenDocumentDetailAsync(Guid documentId)
    {
        NavigationManager.NavigateTo("/document-detail/" + documentId);
        await Task.CompletedTask;
    }
}