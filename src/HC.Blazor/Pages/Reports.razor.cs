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
using HC.Reports;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.BlobStoring;
using HC.Blazor.BlobStoring;

namespace HC.Blazor.Pages;

public partial class Reports
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<ReportDto> DataGridRef { get; set; }

    private IReadOnlyList<ReportDto> ReportList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateReport { get; set; }

    private bool CanEditReport { get; set; }

    private bool CanDeleteReport { get; set; }

    private ReportCreateDto NewReport { get; set; }

    private Validations NewReportValidations { get; set; } = new();
    private ReportUpdateDto EditingReport { get; set; }
    private FilePicker CreateImageFilePicker { get; set; } = new();
    private FilePicker EditImageFilePicker { get; set; } = new();
    private int CreateImagePickerKey { get; set; }
    private int EditImagePickerKey { get; set; }

    [Inject]
    protected IBlobDisplayUrlProvider BlobDisplayUrlProvider { get; set; } = default!;

    private Validations EditingReportValidations { get; set; } = new();
    private Guid EditingReportId { get; set; }

    private Modal CreateReportModal { get; set; } = new();
    private Modal EditReportModal { get; set; } = new();
    private GetReportsInput Filter { get; set; }

    private DataGridEntityActionsColumn<ReportDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "report-create-tab";
    protected string SelectedEditTab = "report-edit-tab";
    private ReportDto? SelectedReport;

    private List<ReportDto> SelectedReports { get; set; } = new();
    private bool AllReportsSelected { get; set; }
    
    [Inject]
    protected IBlobContainer BlobContainer { get; set; } = default!;

    public Reports()
    {
        NewReport = new ReportCreateDto();
        EditingReport = new ReportUpdateDto();
        Filter = new GetReportsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        ReportList = new List<ReportDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Reports"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewReport"], async () => {
            await OpenCreateReportModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.ReportsCreate);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(ReportDto report)
    {
        DataGridRef.ToggleDetailRow(report, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<ReportDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteReport;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<ReportDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateReport = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.ReportsCreate);
        CanEditReport = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.ReportsEdit);
        CanDeleteReport = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.ReportsDelete);
    }

    private async Task GetReportsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await ReportsAppService.GetListAsync(Filter);
        ReportList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetReportsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await ReportsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/reports/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Name={HttpUtility.UrlEncode(Filter.Name)}&Url={HttpUtility.UrlEncode(Filter.Url)}&SortOrderMin={Filter.SortOrderMin}&SortOrderMax={Filter.SortOrderMax}&Image={HttpUtility.UrlEncode(Filter.Image)}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<ReportDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetReportsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateReportModalAsync()
    {
        NewReport = new ReportCreateDto
        {
        };
        CreateImagePickerKey++;
        SelectedCreateTab = "report-create-tab";
        await NewReportValidations.ClearAll();
        await CreateReportModal.Show();
    }

    private async Task CloseCreateReportModalAsync()
    {
        NewReport = new ReportCreateDto
        {
        };
        CreateImagePickerKey++;
        await CreateReportModal.Hide();
    }

    private async Task OpenEditReportModalAsync(ReportDto input)
    {
        SelectedEditTab = "report-edit-tab";
        var report = await ReportsAppService.GetAsync(input.Id);
        EditingReportId = report.Id;
        EditingReport = ObjectMapper.Map<ReportDto, ReportUpdateDto>(report);
        EditImagePickerKey++;
        await EditingReportValidations.ClearAll();
        await EditReportModal.Show();
    }

    private async Task DeleteReportAsync(ReportDto input)
    {
        try
        {
            await ReportsAppService.DeleteAsync(input.Id);
            await GetReportsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CreateReportAsync()
    {
        try
        {
            if (await NewReportValidations.ValidateAll() == false)
            {
                return;
            }

            await ReportsAppService.CreateAsync(NewReport);
            await GetReportsAsync();
            await CloseCreateReportModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditReportModalAsync()
    {
        EditImagePickerKey++;
        await EditReportModal.Hide();
    }

    private async Task UpdateReportAsync()
    {
        try
        {
            if (await EditingReportValidations.ValidateAll() == false)
            {
                return;
            }

            await ReportsAppService.UpdateAsync(EditingReportId, EditingReport);
            await GetReportsAsync();
            await EditReportModal.Hide();
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

    protected virtual async Task OnUrlChangedAsync(string? url)
    {
        Filter.Url = url;
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

    protected virtual async Task OnImageChangedAsync(string? image)
    {
        Filter.Image = image;
        await SearchAsync();
    }

    protected virtual async Task OnCreateImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            await UploadImageFileAsync(e.Files.First(), false);
            return;
        }

        NewReport.Image = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task OnEditImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            await UploadImageFileAsync(e.Files.First(), true);
            return;
        }

        EditingReport.Image = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task UploadImageFileAsync(IFileEntry file, bool isEditMode)
    {
        try
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                await Message.Error(L["OnlyImageFilesAllowed"]);
                if (isEditMode)
                {
                    await EditImageFilePicker.Clear();
                }
                else
                {
                    await CreateImageFilePicker.Clear();
                }

                return;
            }

            if (file.Size > 52428800)
            {
                await Message.Error(L["FileSizeTooLarge"]);
                if (isEditMode)
                {
                    await EditImageFilePicker.Clear();
                }
                else
                {
                    await CreateImageFilePicker.Clear();
                }

                return;
            }

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            var filePath = $"report-images/{Guid.NewGuid()}_{file.Name}";
            await BlobContainer.SaveAsync(filePath, memoryStream.ToArray());

            if (isEditMode)
            {
                EditingReport.Image = filePath;
            }
            else
            {
                NewReport.Image = filePath;
            }

            await Message.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual string GetImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        return BlobDisplayUrlProvider.GetDisplayUrl(imagePath);
    }

    private Task SelectAllItems()
    {
        AllReportsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllReportsSelected = false;
        SelectedReports.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedReportRowsChanged()
    {
        if (SelectedReports.Count != PageSize)
        {
            AllReportsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedReportsAsync()
    {
        var message = AllReportsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedReports.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllReportsSelected)
        {
            await ReportsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await ReportsAppService.DeleteByIdsAsync(SelectedReports.Select(x => x.Id).ToList());
        }

        SelectedReports.Clear();
        AllReportsSelected = false;
        await GetReportsAsync();
    }
}