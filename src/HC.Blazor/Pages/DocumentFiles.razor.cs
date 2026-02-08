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
using HC.DocumentFiles;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class DocumentFiles
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentFileWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<DocumentFileWithNavigationPropertiesDto> DocumentFileList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateDocumentFile { get; set; }

    private bool CanEditDocumentFile { get; set; }

    private bool CanDeleteDocumentFile { get; set; }

    private DocumentFileCreateDto NewDocumentFile { get; set; }

    private Validations NewDocumentFileValidations { get; set; } = new();
    private DocumentFileUpdateDto EditingDocumentFile { get; set; }

    private Validations EditingDocumentFileValidations { get; set; } = new();
    private Guid EditingDocumentFileId { get; set; }

    private Modal CreateDocumentFileModal { get; set; } = new();
    private Modal EditDocumentFileModal { get; set; } = new();
    private GetDocumentFilesInput Filter { get; set; }

    private DataGridEntityActionsColumn<DocumentFileWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "documentFile-create-tab";
    protected string SelectedEditTab = "documentFile-edit-tab";
    private DocumentFileWithNavigationPropertiesDto? SelectedDocumentFile;

    private IReadOnlyList<LookupDto<Guid>> DocumentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<DocumentFileWithNavigationPropertiesDto> SelectedDocumentFiles { get; set; } = new();
    private bool AllDocumentFilesSelected { get; set; }

    public DocumentFiles()
    {
        NewDocumentFile = new DocumentFileCreateDto();
        EditingDocumentFile = new DocumentFileUpdateDto();
        Filter = new GetDocumentFilesInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        DocumentFileList = new List<DocumentFileWithNavigationPropertiesDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentFiles"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewDocumentFile"], async () => {
            await OpenCreateDocumentFileModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.DocumentFiles.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(DocumentFileWithNavigationPropertiesDto documentFile)
    {
        DataGridRef.ToggleDetailRow(documentFile, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentFileWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocumentFile;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<DocumentFileWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDocumentFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentFiles.Create);
        CanEditDocumentFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentFiles.Edit);
        CanDeleteDocumentFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentFiles.Delete);
    }

    private async Task GetDocumentFilesAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await DocumentFilesAppService.GetListAsync(Filter);
        DocumentFileList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentFilesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await DocumentFilesAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/document-files/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Name={HttpUtility.UrlEncode(Filter.Name)}&Path={HttpUtility.UrlEncode(Filter.Path)}&Hash={HttpUtility.UrlEncode(Filter.Hash)}&IsSigned={Filter.IsSigned}&UploadedAtMin={Filter.UploadedAtMin?.ToString("O")}&UploadedAtMax={Filter.UploadedAtMax?.ToString("O")}&DocumentId={Filter.DocumentId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentFileWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentFilesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateDocumentFileModalAsync()
    {
        NewDocumentFile = new DocumentFileCreateDto
        {
            UploadedAt = DateTime.Now,
        };
        SelectedCreateTab = "documentFile-create-tab";
        await NewDocumentFileValidations.ClearAll();
        await CreateDocumentFileModal.Show();
    }

    private async Task CloseCreateDocumentFileModalAsync()
    {
        NewDocumentFile = new DocumentFileCreateDto
        {
            UploadedAt = DateTime.Now,
        };
        await CreateDocumentFileModal.Hide();
    }

    private async Task OpenEditDocumentFileModalAsync(DocumentFileWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "documentFile-edit-tab";
        var documentFile = await DocumentFilesAppService.GetWithNavigationPropertiesAsync(input.DocumentFile.Id);
        EditingDocumentFileId = documentFile.DocumentFile.Id;
        EditingDocumentFile = ObjectMapper.Map<DocumentFileDto, DocumentFileUpdateDto>(documentFile.DocumentFile);
        await EditingDocumentFileValidations.ClearAll();
        await EditDocumentFileModal.Show();
    }

    private async Task DeleteDocumentFileAsync(DocumentFileWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentFilesAppService.DeleteAsync(input.DocumentFile.Id);
            await GetDocumentFilesAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CreateDocumentFileAsync()
    {
        try
        {
            if (await NewDocumentFileValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentFilesAppService.CreateAsync(NewDocumentFile);
            await GetDocumentFilesAsync();
            await CloseCreateDocumentFileModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditDocumentFileModalAsync()
    {
        await EditDocumentFileModal.Hide();
    }

    private async Task UpdateDocumentFileAsync()
    {
        try
        {
            if (await EditingDocumentFileValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentFilesAppService.UpdateAsync(EditingDocumentFileId, EditingDocumentFile);
            await GetDocumentFilesAsync();
            await EditDocumentFileModal.Hide();
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

    protected virtual async Task OnNameChangedAsync(string? name)
    {
        Filter.Name = name;
        await SearchAsync();
    }

    protected virtual async Task OnPathChangedAsync(string? path)
    {
        Filter.Path = path;
        await SearchAsync();
    }

    protected virtual async Task OnHashChangedAsync(string? hash)
    {
        Filter.Hash = hash;
        await SearchAsync();
    }

    protected virtual async Task OnIsSignedChangedAsync(bool? isSigned)
    {
        Filter.IsSigned = isSigned;
        await SearchAsync();
    }

    protected virtual async Task OnUploadedAtMinChangedAsync(DateTime? uploadedAtMin)
    {
        Filter.UploadedAtMin = uploadedAtMin.HasValue ? uploadedAtMin.Value.Date : uploadedAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnUploadedAtMaxChangedAsync(DateTime? uploadedAtMax)
    {
        Filter.UploadedAtMax = uploadedAtMax.HasValue ? uploadedAtMax.Value.Date.AddDays(1).AddSeconds(-1) : uploadedAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnDocumentIdChangedAsync(Guid? documentId)
    {
        Filter.DocumentId = documentId;
        await SearchAsync();
    }

    private async Task GetDocumentCollectionLookupAsync(string? newValue = null)
    {
        DocumentsCollection = (await DocumentFilesAppService.GetDocumentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllDocumentFilesSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllDocumentFilesSelected = false;
        SelectedDocumentFiles.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedDocumentFileRowsChanged()
    {
        if (SelectedDocumentFiles.Count != PageSize)
        {
            AllDocumentFilesSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedDocumentFilesAsync()
    {
        var message = AllDocumentFilesSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedDocumentFiles.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllDocumentFilesSelected)
        {
            await DocumentFilesAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await DocumentFilesAppService.DeleteByIdsAsync(SelectedDocumentFiles.Select(x => x.DocumentFile.Id).ToList());
        }

        SelectedDocumentFiles.Clear();
        AllDocumentFilesSelected = false;
        await GetDocumentFilesAsync();
    }
}