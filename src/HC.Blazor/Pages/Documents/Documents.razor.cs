using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using System.IO;
using System.Web;
using Blazorise;
using Blazorise.DataGrid;
using HC.DocumentHistories;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.Documents;
using HC.DocumentFiles;
using HC.Permissions;
using HC.Shared;
using Volo.Abp.BlobStoring;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using HC.Departments;
using System.Threading;
using Excubo.Blazor.TreeViews;
using Microsoft.AspNetCore.Components.Web;
using HC.MasterDatas;
using Volo.Abp.AspNetCore.Components.Messages;
using Microsoft.JSInterop;
using Volo.Abp.Identity;

namespace HC.Blazor.Pages.Documents;

public partial class Documents : IDisposable
{
    [Inject] private IJSRuntime JSRuntime { get; set; } = default!;

    // Dynamic page title based on sourceType
    protected string PageTitle { get; set; } = string.Empty;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentWithNavigationPropertiesDto> DataGridRef { get; set; } = new();

    private IReadOnlyList<DocumentWithNavigationPropertiesDto> DocumentList { get; set; } = new List<DocumentWithNavigationPropertiesDto>();

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool IsLoading { get; set; }
    private bool IsExporting { get; set; }

    private bool CanCreateDocument { get; set; }

    private bool CanEditDocument { get; set; }

    private bool CanDeleteDocument { get; set; }
    private bool CanSendDocument { get; set; }
    private bool CanSubmitForSigning { get; set; }
    private bool CanSubmitForApproval { get; set; }
    private bool CanRejectApproval { get; set; }
    private bool CanApproveWithNote { get; set; }

    private GetDocumentsInput Filter { get; set; } = new GetDocumentsInput();
    private string FieldFilterValue { get; set; } = string.Empty;
    private string UnitFilterValue { get; set; } = string.Empty;
    private string StatusFilterValue { get; set; } = string.Empty;
    private string TypeFilterValue { get; set; } = string.Empty;
    private string UrgencyLevelFilterValue { get; set; } = string.Empty;
    private string SecrecyLevelFilterValue { get; set; } = string.Empty;
    private IReadOnlyList<LookupDto<Guid>> UnitsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> StatusMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> TypeMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> UrgencyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> SecrecyLevelMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> FieldMasterDataCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<DocumentWithNavigationPropertiesDto> SelectedDocuments { get; set; } = new();
    private bool AllDocumentsSelected { get; set; } = false;

    // Add SourceType filter for distinguishing Archive and Personal documents
    private DocumentSourceType? SelectedSourceType { get; set; }

    /// <summary>
    /// Delete / revoke allowed only in Archive view (sourceType = 0). SentToMe (2) and Personal (1) are not allowed.
    /// </summary>
    private bool CanDeleteOrRevokeInArchiveView => SelectedSourceType == DocumentSourceType.Archive;

    /// <summary>
    /// SentToMe inbox: no edit navigation — only view PDF, send, submit for signing.
    /// </summary>
    private bool CanShowEditDocumentButton => SelectedSourceType != DocumentSourceType.SentToMe;

    private Modal SendDocumentModal { get; set; } = new();
    private Modal SubmitApprovalModal { get; set; } = new();
    private Modal ApprovalReviewModal { get; set; } = new();

    // Submit for Signing Modal (reusable component)
    private HC.Blazor.Components.SubmitWorkflowModal.SubmitWorkflowModal SubmitWorkflowModalRef { get; set; } = default!;

    // PDF Viewer Modal
    private Modal DocumentPdfViewerModal { get; set; } = new();
    private string? DocumentPdfFileUrl { get; set; }
    private bool IsDocumentPdfFile { get; set; }
    private Guid? CurrentDocumentPdfDocumentId { get; set; }
    private IReadOnlyList<DocumentHistoryWithNavigationPropertiesDto> PreviewDocumentHistories { get; set; } = new List<DocumentHistoryWithNavigationPropertiesDto>();
    private string? ApprovalPdfFileUrl { get; set; }
    private bool IsApprovalPdfFile { get; set; }
    private int PickedPageNumber { get; set; }
    private double PickedPdfX { get; set; }
    private double PickedPdfY { get; set; }
    private bool IsInitializingPdfPicker { get; set; }
    private string ApprovalActionNote { get; set; } = string.Empty;
    private HC.Blazor.Components.ProjectTaskCreateModal.ProjectTaskCreateModal CreateTaskModalRef { get; set; } = default!;
    private DotNetObjectReference<Documents>? DotNetRef { get; set; }

    private CancellationTokenSource? SearchDebounceCts { get; set; }

    // Track previous URL and sourceType to detect changes
    private string? previousAbsoluteUrl;
    private string? previousSourceTypeValue;

    /// <summary>
    /// After first DataGrid ReadData; avoids CheckForUrlChangesAsync racing before snapshot matches navigation.
    /// </summary>
    private bool _documentListHydrated;

    private bool _processingApprovalReviewDeepLink;

    public Documents()
    {
        DepartmentList = new List<DepartmentDto>();
        DepartmentTreeViews = new List<DepartmentTreeView>();
        AllDepartmentsFlat = new List<DepartmentTreeView>();
        AllDepartmentsForSelect2 = new List<DepartmentTreeView>();

        // Initialize default title - will be set in OnInitializedAsync
        PageTitle = string.Empty;
    }

    protected override Task OnInitializedAsync()
    {
        // Update page title from query parameter during initialization
        UpdateSourceTypeFromQuery();
        // Initialize before first LocationChanged so missing previousSourceTypeValue does not look like a "tab change"
        SyncNavigationSnapshotForUrlChecks();
        Logger.LogInformation($"OnInitializedAsync: PageTitle={PageTitle}, SelectedSourceType={SelectedSourceType}");
        return Task.CompletedTask;
    }

    private void SyncNavigationSnapshotForUrlChecks()
    {
        var currentUrl = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        previousAbsoluteUrl = currentUrl.ToString();
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(currentUrl.Query);
        previousSourceTypeValue = query.TryGetValue("sourceType", out var st) ? st.ToString() : null;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {

        if (firstRender)
        {
            NavigationManager.LocationChanged += OnLocationChanged;

            await Task.WhenAll(
                HydrateFromPageBootstrapAsync(),
                GetDepartmentsAsync());
            await SetToolbarItemsAsync();

            BreadcrumbItems.Clear();
            await SetBreadcrumbItemsAsync();


            await InvokeAsync(StateHasChanged);
        }
        else
        {
            // Also check for URL changes on every render as backup
            await CheckForUrlChangesAsync();
        }

        Logger.LogInformation("Documents OnAfterRenderAsync end");
    }

    /// <summary>
    /// Handle URL location changes to detect sourceType parameter changes
    /// </summary>
    private async void OnLocationChanged(object? sender, Microsoft.AspNetCore.Components.Routing.LocationChangedEventArgs e)
    {
        try
        {
            Logger.LogInformation($"LocationChanged event fired: {e.Location}");

            var currentUrl = NavigationManager.ToAbsoluteUri(NavigationManager.Uri).ToString();
            var currentUri = new Uri(currentUrl);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(currentUri.Query);
            var currentSourceTypeValue = query.TryGetValue("sourceType", out var st) ? st.ToString() : null;

            // Check if sourceType parameter changed
            if (previousSourceTypeValue != currentSourceTypeValue)
            {
                Logger.LogInformation($"LocationChanged: sourceType changed from '{previousSourceTypeValue}' to '{currentSourceTypeValue}', updating UI");

                await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
                try
                {
                    // Update sourceType and page title
                    UpdateSourceTypeFromQuery();

                    // Update breadcrumb items with new title
                    BreadcrumbItems.Clear();
                    await SetBreadcrumbItemsAsync();

                    // Reload documents
                    await GetDocumentsAsync();
                }
                finally
                {
                    await BlockUiService.UnBlock();
                }

                // Update previous values
                previousAbsoluteUrl = currentUrl;
                previousSourceTypeValue = currentSourceTypeValue;

                // Force UI refresh
                await InvokeAsync(StateHasChanged);
                Logger.LogInformation("LocationChanged: UI updated successfully");
                await TryProcessApprovalReviewDeepLinkAsync();
            }
            else
            {
                previousAbsoluteUrl = currentUrl;
                Logger.LogInformation("LocationChanged: sourceType unchanged, no update needed");
                await TryProcessApprovalReviewDeepLinkAsync();
                await InvokeAsync(StateHasChanged);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in OnLocationChanged");
        }
    }

    /// <summary>
    /// Check if URL or sourceType parameter has changed and update UI accordingly
    /// </summary>
    private async Task CheckForUrlChangesAsync()
    {
        if (!_documentListHydrated)
        {
            return;
        }

        try
        {
            var currentUrl = NavigationManager.ToAbsoluteUri(NavigationManager.Uri).ToString();

            // Check if URL changed
            if (previousAbsoluteUrl != currentUrl)
            {
                Logger.LogInformation($"URL changed from {previousAbsoluteUrl} to {currentUrl}");

                var currentUri = new Uri(currentUrl);
                var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(currentUri.Query);
                var currentSourceTypeValue = query.TryGetValue("sourceType", out var st) ? st.ToString() : null;

                // Check if sourceType parameter changed
                if (previousSourceTypeValue != currentSourceTypeValue)
                {
                    Logger.LogInformation($"sourceType changed from '{previousSourceTypeValue}' to '{currentSourceTypeValue}', updating UI");

                    await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
                    try
                    {
                        // Update sourceType and page title
                        UpdateSourceTypeFromQuery();
                        Logger.LogInformation($"After UpdateSourceTypeFromQuery: PageTitle={PageTitle}");

                        // Update breadcrumb items with new title
                        BreadcrumbItems.Clear();
                        await SetBreadcrumbItemsAsync();
                        Logger.LogInformation($"After SetBreadcrumbItemsAsync: BreadcrumbItems count={BreadcrumbItems.Count}");

                        // Reload documents
                        await GetDocumentsAsync();
                    }
                    finally
                    {
                        await BlockUiService.UnBlock();
                    }

                    // Update previous values
                    previousAbsoluteUrl = currentUrl;
                    previousSourceTypeValue = currentSourceTypeValue;

                    // Force UI refresh
                    await InvokeAsync(StateHasChanged);
                    Logger.LogInformation("UI updated successfully after sourceType change");
                }
                else
                {
                    // URL changed but sourceType didn't, just update previous URL
                    previousAbsoluteUrl = currentUrl;
                    Logger.LogInformation("URL changed but sourceType is the same, no update needed");
                    await TryProcessApprovalReviewDeepLinkAsync();
                }
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in CheckForUrlChangesAsync");
        }
    }

    /// <summary>
    /// Update SourceType filter from query parameter
    /// </summary>
    private void UpdateSourceTypeFromQuery()
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

        if (query.TryGetValue("sourceType", out var sourceTypeValue) && int.TryParse(sourceTypeValue, out var sourceTypeInt))
        {
            SelectedSourceType = (DocumentSourceType)sourceTypeInt;
            Filter.SourceType = SelectedSourceType;

            if (SelectedSourceType == DocumentSourceType.Personal)
            {
                Filter.CreatorId = CurrentUser.Id;
                PageTitle = L["PersonalDocuments"];
            }
            else if (SelectedSourceType == DocumentSourceType.SentToMe)
            {
                Filter.CreatorId = null;
                PageTitle = L["DocumentsSentToMe"];
            }
            else if (SelectedSourceType == DocumentSourceType.Archive)
            {
                Filter.CreatorId = null;
                PageTitle = L["ArchiveDocuments"];
            }
            else
            {
                Filter.CreatorId = null;
                PageTitle = L["Documents"];
            }

            Logger.LogInformation($"Updated sourceType to {SelectedSourceType}, PageTitle: {PageTitle}");
        }
        else
        {
            // manage-documents without query: default to Archive (văn thư lưu trữ)
            SelectedSourceType = DocumentSourceType.Archive;
            Filter.SourceType = DocumentSourceType.Archive;
            Filter.CreatorId = null;
            PageTitle = L["ArchiveDocuments"];
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

        if (CanCreateDocument)
        {
            Toolbar.AddButton(L["NewDocument"], () => {
                var sourceTypeParam = SelectedSourceType.HasValue ? $"?sourceType={(int)SelectedSourceType.Value}" : "";
                NavigationManager.NavigateTo("/document-detail" + sourceTypeParam);
                return Task.CompletedTask;
            }, IconName.Add, requiredPolicyName: HCPermissions.Documents.Create);
        }

        return ValueTask.CompletedTask;
    }
    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentWithNavigationPropertiesDto> rowSelectableEventArgs) =>
        rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick
        && CanDeleteDocument
        && CanDeleteOrRevokeInArchiveView;

    private async Task SetPermissionsAsync()
    {
        // Run all permission checks in parallel to cut 7 sequential round-trips on first paint.
        // Kept for code paths that still need local permission refresh (e.g. navigating back).
        var createTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Create);
        var editTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Edit);
        var sendTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Send);
        var submitForSigningTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.SubmitForSigning);
        var submitForApprovalTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.SubmitForApproval);
        var rejectApprovalTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.RejectApproval);
        var approveWithNoteTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.ApproveWithNote);
        var deleteTask = AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Delete);

        await Task.WhenAll(
            createTask, editTask, sendTask, submitForSigningTask,
            submitForApprovalTask, rejectApprovalTask, approveWithNoteTask, deleteTask);

        CanCreateDocument = createTask.Result;
        CanEditDocument = editTask.Result;
        CanSendDocument = sendTask.Result;
        CanSubmitForSigning = submitForSigningTask.Result;
        CanSubmitForApproval = submitForApprovalTask.Result;
        CanRejectApproval = rejectApprovalTask.Result;
        CanApproveWithNote = approveWithNoteTask.Result;
        CanDeleteDocument = deleteTask.Result;
    }

    /// <summary>
    /// M3: pull permissions + lookups from the new <c>documents/page-bootstrap</c> endpoint
    /// so first paint needs one HTTP round-trip instead of ~14.
    /// </summary>
    private async Task HydrateFromPageBootstrapAsync()
    {
        try
        {
            var bootstrap = await DocumentsAppService.GetPageBootstrapAsync(new GetDocumentsPageBootstrapInput
            {
                LookupPageSize = 200,
                IncludeUnits = true,
                IncludeWorkflows = false,
                MasterDataTypes = new List<string>
                {
                    MasterDataType.Status.GetTypeValue(),
                    MasterDataType.DocumentType.GetTypeValue(),
                    MasterDataType.UrgencyLevel.GetTypeValue(),
                    MasterDataType.SecrecyLevel.GetTypeValue(),
                    MasterDataType.Field.GetTypeValue()
                }
            });

            CanCreateDocument = bootstrap.Permissions.CanCreate;
            CanEditDocument = bootstrap.Permissions.CanEdit;
            CanDeleteDocument = bootstrap.Permissions.CanDelete;
            CanSendDocument = bootstrap.Permissions.CanSend;
            CanSubmitForSigning = bootstrap.Permissions.CanSubmitForSigning;
            CanSubmitForApproval = bootstrap.Permissions.CanSubmitForApproval;
            CanRejectApproval = bootstrap.Permissions.CanRejectApproval;
            CanApproveWithNote = bootstrap.Permissions.CanApproveWithNote;

            StatusMasterDataCollection = GetBootstrapLookup(bootstrap, MasterDataType.Status);
            TypeMasterDataCollection = GetBootstrapLookup(bootstrap, MasterDataType.DocumentType);
            UrgencyLevelMasterDataCollection = GetBootstrapLookup(bootstrap, MasterDataType.UrgencyLevel);
            SecrecyLevelMasterDataCollection = GetBootstrapLookup(bootstrap, MasterDataType.SecrecyLevel);
            FieldMasterDataCollection = GetBootstrapLookup(bootstrap, MasterDataType.Field);
            UnitsCollection = bootstrap.Units ?? new List<LookupDto<Guid>>();
        }
        catch (Exception ex)
        {
            // Fallback to the legacy per-call path if the bundle endpoint fails (older server, auth hiccup, etc.).
            Logger.LogWarning(ex, "Documents page-bootstrap failed; falling back to per-call lookups");
            await SetPermissionsAsync();
            await Task.WhenAll(
                GetStatusMasterDataLookupAsync(StatusMasterDataCollection, string.Empty, CancellationToken.None),
                GetTypeMasterDataLookupAsync(TypeMasterDataCollection, string.Empty, CancellationToken.None),
                GetUrgencyLevelMasterDataLookupAsync(UrgencyLevelMasterDataCollection, string.Empty, CancellationToken.None),
                GetSecrecyLevelMasterDataLookupAsync(SecrecyLevelMasterDataCollection, string.Empty, CancellationToken.None),
                GetFieldMasterDataLookupAsync(FieldMasterDataCollection, string.Empty, CancellationToken.None),
                GetUnitLookupAsync(UnitsCollection, string.Empty, CancellationToken.None));
        }
    }

    private static List<LookupDto<Guid>> GetBootstrapLookup(DocumentsPageBootstrapDto bootstrap, MasterDataType type)
    {
        return bootstrap.MasterDataLookups.TryGetValue(type.GetTypeValue(), out var list) && list != null
            ? list
            : new List<LookupDto<Guid>>();
    }

    private async Task GetDocumentsAsync()
    {
        Logger.LogInformation("GetDocumentsAsync start");
        IsLoading = true;
        try
        {
            Filter.MaxResultCount = PageSize;
            Filter.SkipCount = (CurrentPage - 1) * PageSize;
            Filter.Sorting = CurrentSorting;
            var result = await DocumentsAppService.GetListAsync(Filter);
            DocumentList = result.Items;
            TotalCount = (int)result.TotalCount;
            await ClearSelection();
        }
        finally
        {
            IsLoading = false;
            Logger.LogInformation("GetDocumentsAsync end");
        }
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task OnFilterTextChangedAsync(string? filterText)
    {
        Filter.FilterText = filterText;
        await DebouncedSearchAsync();
    }

    private async Task DebouncedSearchAsync()
    {
        var previous = SearchDebounceCts;
        SearchDebounceCts = new CancellationTokenSource();
        previous?.Cancel();
        previous?.Dispose();
        var token = SearchDebounceCts.Token;

        try
        {
            await Task.Delay(350, token);
        }
        catch (TaskCanceledException)
        {
            return;
        }

        if (token.IsCancellationRequested)
        {
            return;
        }

        await SearchAsync();
    }

    private async Task DownloadAsExcelAsync()
    {
        if (IsExporting)
        {
            await UiMessageService.Info(L["Exporting"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            return;
        }

        IsExporting = true;
        await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
        try
        {
            await UiMessageService.Info(L["Exporting"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            var token = (await DocumentsAppService.GetDownloadTokenAsync()).Token;
            var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
            var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
            if (!culture.IsNullOrEmpty())
            {
                culture = "&culture=" + culture;
            }

            await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
            NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/documents/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&No={HttpUtility.UrlEncode(Filter.No)}&Title={HttpUtility.UrlEncode(Filter.Title)}&CurrentStatus={HttpUtility.UrlEncode(Filter.CurrentStatus)}&CompletedTimeMin={Filter.CompletedTimeMin?.ToString("O")}&CompletedTimeMax={Filter.CompletedTimeMax?.ToString("O")}&StorageNumber={HttpUtility.UrlEncode(Filter.StorageNumber)}&IncommingDateMin={Filter.IncommingDateMin?.ToString("O")}&IncommingDateMax={Filter.IncommingDateMax?.ToString("O")}&FieldId={Filter.FieldId}&UnitId={Filter.UnitId}&WorkflowId={Filter.WorkflowId}&StatusId={Filter.StatusId}&TypeId={Filter.TypeId}&UrgencyLevelId={Filter.UrgencyLevelId}&SecrecyLevelId={Filter.SecrecyLevelId}&SourceType={Filter.SourceType}&CreatorId={Filter.CreatorId}", forceLoad: true);
        }
        finally
        {
            await BlockUiService.UnBlock();
            IsExporting = false;
        }
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentWithNavigationPropertiesDto> e)
    {
        try
        {
            CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
            CurrentPage = e.Page;
            await GetDocumentsAsync();
        }
        finally
        {
            _documentListHydrated = true;
        }

        await TryProcessApprovalReviewDeepLinkAsync();
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
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnTitleChangedAsync(string? title)
    {
        Filter.Title = title;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnCurrentStatusChangedAsync(string? currentStatus)
    {
        Filter.CurrentStatus = currentStatus;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnCompletedTimeMinChangedAsync(DateTime? completedTimeMin)
    {
        Filter.CompletedTimeMin = completedTimeMin.HasValue ? completedTimeMin.Value.Date : completedTimeMin;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnCompletedTimeMaxChangedAsync(DateTime? completedTimeMax)
    {
        Filter.CompletedTimeMax = completedTimeMax.HasValue ? completedTimeMax.Value.Date.AddDays(1).AddSeconds(-1) : completedTimeMax;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnStorageNumberChangedAsync(string? storageNumber)
    {
        Filter.StorageNumber = storageNumber;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnIncommingDateMinChangedAsync(DateTime? incommingDateMin)
    {
        Filter.IncommingDateMin = incommingDateMin.HasValue ? incommingDateMin.Value.Date : incommingDateMin;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnIncommingDateMaxChangedAsync(DateTime? incommingDateMax)
    {
        Filter.IncommingDateMax = incommingDateMax.HasValue ? incommingDateMax.Value.Date.AddDays(1).AddSeconds(-1) : incommingDateMax;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnFieldIdChangedAsync(string? fieldId)
    {
        Filter.FieldId = Guid.TryParse(fieldId, out var parsedFieldId) ? parsedFieldId : null;
        FieldFilterValue = fieldId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnUnitIdChangedAsync(string? unitId)
    {
        Filter.UnitId = Guid.TryParse(unitId, out var parsedUnitId) ? parsedUnitId : null;
        UnitFilterValue = unitId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnWorkflowIdChangedAsync(Guid? workflowId)
    {
        Filter.WorkflowId = workflowId;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnStatusIdChangedAsync(string? statusId)
    {
        Filter.StatusId = Guid.TryParse(statusId, out var parsedStatusId) ? parsedStatusId : null;
        StatusFilterValue = statusId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnTypeIdChangedAsync(string? typeId)
    {
        Filter.TypeId = Guid.TryParse(typeId, out var parsedTypeId) ? parsedTypeId : null;
        TypeFilterValue = typeId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnUrgencyLevelIdChangedAsync(string? urgencyLevelId)
    {
        Filter.UrgencyLevelId = Guid.TryParse(urgencyLevelId, out var parsedUrgencyLevelId) ? parsedUrgencyLevelId : null;
        UrgencyLevelFilterValue = urgencyLevelId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnSecrecyLevelIdChangedAsync(string? secrecyLevelId)
    {
        Filter.SecrecyLevelId = Guid.TryParse(secrecyLevelId, out var parsedSecrecyLevelId) ? parsedSecrecyLevelId : null;
        SecrecyLevelFilterValue = secrecyLevelId ?? string.Empty;
        await DebouncedSearchAsync();
    }

    protected virtual async Task OnSourceTypeChangedAsync(DocumentSourceType? sourceType)
    {
        Filter.SourceType = sourceType;
        Filter.CreatorId = sourceType == DocumentSourceType.Personal ? CurrentUser.Id : null;
        await DebouncedSearchAsync();
    }

    private const int DocumentLookupPageSize = 200;

    private async Task<List<LookupDto<Guid>>> LoadMasterDataLookupForGridAsync(MasterDataType type, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            return await DocumentsPageLookupCache.GetMasterDataLookupAsync(
                type.GetTypeValue(),
                () => MasterDatasAppService.GetListAsync(new GetMasterDatasInput
                {
                    Type = type.GetTypeValue(),
                    MaxResultCount = DocumentLookupPageSize,
                    SkipCount = 0
                }));
        }

        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = type.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = DocumentLookupPageSize,
            SkipCount = 0
        });
        return result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetTypeMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var list = await LoadMasterDataLookupForGridAsync(MasterDataType.DocumentType, filter);
        TypeMasterDataCollection = list;
        return list;
    }

    private async Task<List<LookupDto<Guid>>> GetUrgencyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var list = await LoadMasterDataLookupForGridAsync(MasterDataType.UrgencyLevel, filter);
        UrgencyLevelMasterDataCollection = list;
        return list;
    }

    private async Task<List<LookupDto<Guid>>> GetSecrecyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var list = await LoadMasterDataLookupForGridAsync(MasterDataType.SecrecyLevel, filter);
        SecrecyLevelMasterDataCollection = list;
        return list;
    }

    private async Task<List<LookupDto<Guid>>> GetFieldMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var list = await LoadMasterDataLookupForGridAsync(MasterDataType.Field, filter);
        FieldMasterDataCollection = list;
        return list;
    }

    private async Task<List<LookupDto<Guid>>> GetStatusMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var list = await LoadMasterDataLookupForGridAsync(MasterDataType.Status, filter);
        StatusMasterDataCollection = list;
        return list;
    }

    private async Task<List<LookupDto<Guid>>> GetUnitLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(filter))
        {
            var list = await DocumentsPageLookupCache.GetUnitsLookupAsync(() =>
                DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = "", MaxResultCount = DocumentLookupPageSize }));
            UnitsCollection = list;
            return list;
        }

        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = filter, MaxResultCount = DocumentLookupPageSize });
        if (result.Items is List<LookupDto<Guid>> unitList)
        {
            UnitsCollection = unitList;
            return unitList;
        }
        var materialized = result.Items.ToList();
        UnitsCollection = materialized;
        return materialized;
    }

    private static bool IsDocumentApproved(DocumentWithNavigationPropertiesDto item)
    {
        return string.Equals(item.Status?.Code, DocumentStatusCode.DA_PHE_DUYET.GetCode(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Submit-for-approval is only for documents whose status display name is "tạo mới" (master data label).
    /// </summary>
    private static bool IsDocumentStatusTextCreateNew(DocumentWithNavigationPropertiesDto item)
    {
        return string.Equals(item.Status?.Code, DocumentStatusCode.MOI.GetCode(), StringComparison.OrdinalIgnoreCase);
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
        if (!CanDeleteOrRevokeInArchiveView)
        {
            await UiMessageService.Warn(L["DeleteRevokeOnlyInArchiveView"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            return;
        }

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
        var sourceTypeParam = SelectedSourceType.HasValue ? $"?sourceType={(int)SelectedSourceType.Value}" : "";
        NavigationManager.NavigateTo("/document-detail/" + documentId + sourceTypeParam);
        await Task.CompletedTask;
    }

    #region Submit for Signing (Workflow Modal)

    private async Task ShowSubmitWorkflowModalAsync(DocumentWithNavigationPropertiesDto document)
    {
        if (SubmitWorkflowModalRef != null)
        {
            await SubmitWorkflowModalRef.ShowAsync(document);
        }
    }

    private async Task OnSubmitWorkflowCompletedAsync()
    {
        await GetDocumentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private Task OnSubmitWorkflowClosedAsync()
    {
        return Task.CompletedTask;
    }

    #endregion

    #region Send Document

    private List<DepartmentTreeView> DepartmentTreeViews { get; set; } = new();
    private IReadOnlyList<DepartmentDto> DepartmentList { get; set; } = new List<DepartmentDto>();
    private List<DepartmentTreeView> AllDepartmentsFlat { get; set; } = new List<DepartmentTreeView>();
    private List<DepartmentTreeView> AllDepartmentsForSelect2 { get; set; } = new List<DepartmentTreeView>();
    private SendDocumentInput SendDocumentInput { get; set; } = new();
    private DocumentWithNavigationPropertiesDto DocumentToSend { get; set; } = new();
    private bool IsPersonal { get; set; } = false;
    private IReadOnlyList<LookupDto<Guid>> RecipientsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> LeaderUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<LookupDto<Guid>> SelectedRecipients { get; set; } = new();
    private List<LookupDto<Guid>> SelectedLeaderUser { get; set; } = new();
    private List<DepartmentTreeView> SelectedDepartments { get; set; } = new();
    private DepartmentTreeView SelectedItem { get; set; } = new();
    private Guid? SelectedLeaderUserId { get; set; }
    private string SubmitApprovalMessage { get; set; } = string.Empty;
    private DocumentWithNavigationPropertiesDto DocumentToSubmitApproval { get; set; } = new();
    private DocumentWithNavigationPropertiesDto ApprovalReviewDocument { get; set; } = new();

    // Delete or Revoke Document Modal
    private Modal DeleteOrRevokeModal { get; set; } = new();
    private DocumentWithNavigationPropertiesDto DocumentToDeleteOrRevoke { get; set; } = new();
    private string DeleteOrRevokeOption { get; set; } = "delete"; // "delete" or "revoke"

    
    private async Task ShowSendDocumentModalAsync(DocumentWithNavigationPropertiesDto document)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);  
            DocumentToSend = document;
            IsPersonal = false;
            SelectedDepartments.Clear();
            SelectedRecipients.Clear();
            SendDocumentInput.DocumentId = DocumentToSend.Document.Id;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(SendDocumentModal.Show);
        }
    }

    private void OnSendDocumentTargetChanged(bool personal)
    {
        if (IsPersonal == personal)
        {
            return;
        }

        IsPersonal = personal;
        if (personal)
        {
            SelectedDepartments.Clear();
            SelectedItem = new DepartmentTreeView();
        }
        else
        {
            SelectedRecipients.Clear();
        }
    }

    private async Task<List<LookupDto<Guid>>> GetRecipientsLookupAsync(
    IReadOnlyList<LookupDto<Guid>> source,
    string search,
        CancellationToken cancellationToken)
    {
        var result = await UserDepartmentsAppService.GetIdentityUserLookupAsync(
            new LookupRequestDto { Filter = search }
        );

        return result.Items.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetLeaderUserLookupAsync(
        IReadOnlyList<LookupDto<Guid>> source,
        string search,
        CancellationToken cancellationToken)
    {
        var result = await DocumentsAppService.GetIdentityUserLookupAsync(
            new LookupRequestDto
            {
                Filter = search,
                MaxResultCount = DocumentLookupPageSize,
                SkipCount = 0
            });

        LeaderUsersCollection = result.Items;
        return result.Items.ToList();
    }

    private void OnLeaderUserChanged()
    {
        SelectedLeaderUserId = SelectedLeaderUser.FirstOrDefault()?.Id;
    }

    private async Task CloseSendDocumentModalAsync()
    {
        await InvokeAsync(SendDocumentModal.Hide);
    }

    private async Task ShowSubmitApprovalModalAsync(DocumentWithNavigationPropertiesDto document)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            DocumentToSubmitApproval = document;
            SelectedLeaderUser.Clear();
            SelectedLeaderUserId = null;
            SubmitApprovalMessage = string.Empty;
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(SubmitApprovalModal.Show);
        }
    }

    private async Task CloseSubmitApprovalModalAsync()
    {
        await SubmitApprovalModal.Hide();
        DocumentToSubmitApproval = new();
        SelectedLeaderUser.Clear();
        SelectedLeaderUserId = null;
        SubmitApprovalMessage = string.Empty;
    }

    private async Task SubmitApprovalAsync()
    {
        try
        {
            if (DocumentToSubmitApproval?.Document?.Id == Guid.Empty)
            {
                return;
            }

            if (!SelectedLeaderUserId.HasValue || SelectedLeaderUserId.Value == Guid.Empty)
            {
                await UiMessageService.Warn(L["PleaseSelectApprovalLeader"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await DocumentsAppService.SubmitForApprovalAsync(new SubmitDocumentForApprovalInput
            {
                DocumentId = DocumentToSubmitApproval.Document.Id,
                LeaderUserId = SelectedLeaderUserId.Value,
                Message = string.IsNullOrWhiteSpace(SubmitApprovalMessage) ? null : SubmitApprovalMessage.Trim()
            });

            await UiMessageService.Success(L["DocumentSubmittedForApprovalSuccessfully"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await CloseSubmitApprovalModalAsync();
            await GetDocumentsAsync();
            await InvokeAsync(StateHasChanged);
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

    private async Task SendDocumentAsync()
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            // Validate input
            if (SendDocumentInput.DocumentId == Guid.Empty)
            {
                await UiMessageService.Error(L["DocumentIdIsRequired"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            // Check if sending to personal
            if (IsPersonal)
            {
                if (SelectedRecipients == null || SelectedRecipients.Count == 0)
                {
                    await UiMessageService.Error(L["AtLeastOneRecipientIsRequired"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    return;
                }
                SendDocumentInput.Recipients = SelectedRecipients.Select(x => x.Id).ToList();
                SendDocumentInput.Departments = null;
            }
            else
            {
                if (SelectedDepartments == null || SelectedDepartments.Count == 0)
                {
                    await UiMessageService.Error(L["AtLeastOneDepartmentIsRequired"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    return;
                }
                SendDocumentInput.Departments = SelectedDepartments.Select(x => x.Id).ToList();
                SendDocumentInput.Recipients = null;
            }
            var result = await DocumentsAppService.SendDocumentAsync(SendDocumentInput);

            if (result)
            {
                await UiMessageService.Success(L["DocumentSentSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await GetDocumentsAsync();
                await InvokeAsync(SendDocumentModal.Hide);
            }
            else
            {
                await UiMessageService.Error(L["FailedToSendDocument"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            }
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



    private async Task GetDepartmentsAsync()
    {
        var result = await DepartmentsAppService.GetListAsync(new GetDepartmentsInput
        {
            MaxResultCount = LimitedResultRequestDto.MaxMaxResultCount
        });

        DepartmentList = result.Items.Select(x => x.Department).ToList();

        // Map each DepartmentDto to DepartmentTreeView manually
        var departments = DepartmentList.Select(d => ObjectMapper.Map<DepartmentDto, DepartmentTreeView>(d)).ToList();

        var departmentsDictionary = new Dictionary<string, List<DepartmentTreeView>>();

        // Build dictionary: key = ParentId, value = list of children
        foreach (var department in departments)
        {
            var parentId = department.ParentId ?? string.Empty;

            if (!departmentsDictionary.ContainsKey(parentId))
            {
                departmentsDictionary.Add(parentId, new List<DepartmentTreeView>());
            }

            departmentsDictionary[parentId].Add(department);
        }

        // Set Children for each department: Children = entities where ParentId = this.Id
        foreach (var department in departments)
        {
            var departmentId = department.Id.ToString();
            if (departmentsDictionary.ContainsKey(departmentId))
            {
                department.Children = departmentsDictionary[departmentId];
            }
            else
            {
                department.Children = new List<DepartmentTreeView>();
            }
        }

        if (departmentsDictionary.Any())
        {
            DepartmentTreeViews = departmentsDictionary.ContainsKey(string.Empty) 
                ? departmentsDictionary[string.Empty] 
                : new List<DepartmentTreeView>();
        }
        else
        {
            DepartmentTreeViews = new List<DepartmentTreeView>();
        }

        // Build flat list for dropdown (flatten tree structure)
        AllDepartmentsFlat = FlattenDepartments(DepartmentTreeViews);
        
        // Expand all nodes by default
        ExpandAllNodes(DepartmentTreeViews);
        
        // Create list for Select2 (include root option)
        AllDepartmentsForSelect2 = new List<DepartmentTreeView>();
        // Add root option
        AllDepartmentsForSelect2.Add(new DepartmentTreeView 
        { 
            Id = Guid.Empty, 
            Name = L["Root"].Value,
            TreeLevel = -1 // Special level for root
        });
        // Add all departments
        AllDepartmentsForSelect2.AddRange(AllDepartmentsFlat);
    }


    private void ExpandAllNodes(List<DepartmentTreeView> departments)
    {
        if (departments == null) return;
        
        foreach (var dept in departments)
        {
            dept.Collapsed = false; // Expand this node
            if (dept.Children != null && dept.Children.Any())
            {
                ExpandAllNodes(dept.Children); // Recursively expand children
            }
        }
    }
    private List<DepartmentTreeView> FlattenDepartments(List<DepartmentTreeView> departments, int treeLevel = 0)
    {
        var result = new List<DepartmentTreeView>();
        foreach (var dept in departments)
        {
            // Set tree level for display (don't modify the actual Level property)
            dept.TreeLevel = treeLevel;
            result.Add(dept);
            if (dept.Children != null && dept.Children.Any())
            {
                result.AddRange(FlattenDepartments(dept.Children, treeLevel + 1));
            }
        }
        return result;
    }
    
    // Format department name with dashes based on tree level
    private string GetDepartmentDisplayName(DepartmentTreeView department)
    {
        if (department == null || string.IsNullOrEmpty(department.Name))
            return "";
        
        // Special handling for root option
        if (department.Id == Guid.Empty)
            return department.Name;
            
        // TreeLevel 0 = root level, no dash
        // TreeLevel 1 = one dash, TreeLevel 2 = two dashes, etc.
        var dashes = new string('-', department.TreeLevel);
        return string.IsNullOrEmpty(dashes) ? department.Name : $"{dashes} {department.Name}";
    }
    
    #endregion Send Document

    #region Approval Review

    /// <summary>
    /// Opens approval review modal when URL is <c>/manage-documents?sourceType=2&amp;relatedId=...</c> (e.g. from notification).
    /// </summary>
    private async Task TryProcessApprovalReviewDeepLinkAsync()
    {
        if (_processingApprovalReviewDeepLink || SelectedSourceType != DocumentSourceType.SentToMe)
        {
            return;
        }

        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        if (!query.TryGetValue("relatedId", out var relatedIdValue))
        {
            return;
        }

        if (!Guid.TryParse(relatedIdValue.ToString(), out var documentId))
        {
            return;
        }

        _processingApprovalReviewDeepLink = true;
        try
        {
            var dto = await DocumentsAppService.GetWithNavigationPropertiesAsync(documentId);
            await OpenApprovalReviewModalAsync(dto);
            ReplaceUrlRemoveQueryKey("relatedId");
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Approval review deep link failed for document {DocumentId}", documentId);
            await HandleErrorAsync(ex);
        }
        finally
        {
            _processingApprovalReviewDeepLink = false;
        }
    }

    private void ReplaceUrlRemoveQueryKey(string keyToRemove)
    {
        var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
        var path = uri.GetLeftPart(UriPartial.Path);
        var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
        var pairs = new List<KeyValuePair<string, string?>>();
        foreach (var kv in query)
        {
            if (string.Equals(kv.Key, keyToRemove, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            foreach (var value in kv.Value)
            {
                pairs.Add(new KeyValuePair<string, string?>(kv.Key, value));
            }
        }

        var newUrl = pairs.Count == 0 ? path : Microsoft.AspNetCore.WebUtilities.QueryHelpers.AddQueryString(path, pairs);
        NavigationManager.NavigateTo(newUrl, replace: true);
    }

    /// <summary>
    /// Opens the read-only PDF viewer. For SentToMe (sourceType=2), approval is a separate grid column.
    /// </summary>
    private async Task HandleViewDocumentAsync(DocumentWithNavigationPropertiesDto context)
    {
        await OpenDocumentPdfViewerModalAsync(context);
    }

    private async Task OpenApprovalReviewModalAsync(DocumentWithNavigationPropertiesDto context)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            ApprovalReviewDocument = context;
            ApprovalActionNote = string.Empty;
            PickedPageNumber = 0;
            PickedPdfX = 0;
            PickedPdfY = 0;

            var pdfFileUrl = await LoadPdfDataUrlAsync(context.Document.Id);
            if (string.IsNullOrWhiteSpace(pdfFileUrl))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            ApprovalPdfFileUrl = pdfFileUrl;
            IsApprovalPdfFile = true;
            await LoadPreviewDocumentHistoriesAsync(context.Document.Id);
            await ApprovalReviewModal.Show();
            await InitializeApprovalPdfPickerAsync();
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

    private async Task CloseApprovalReviewModalAsync()
    {
        if (ApprovalReviewModal != null)
        {
            await ApprovalReviewModal.Hide();
        }

        ApprovalReviewDocument = new();
        ApprovalPdfFileUrl = null;
        IsApprovalPdfFile = false;
        PreviewDocumentHistories = new List<DocumentHistoryWithNavigationPropertiesDto>();
        ApprovalActionNote = string.Empty;
        PickedPageNumber = 0;
        PickedPdfX = 0;
        PickedPdfY = 0;
    }

    private async Task InitializeApprovalPdfPickerAsync()
    {
        if (IsInitializingPdfPicker || string.IsNullOrWhiteSpace(ApprovalPdfFileUrl))
        {
            return;
        }

        try
        {
            IsInitializingPdfPicker = true;
            DotNetRef?.Dispose();
            DotNetRef = DotNetObjectReference.Create(this);
            await JSRuntime.InvokeVoidAsync("pdfPick.init", DotNetRef, ApprovalPdfFileUrl, "approval-review-pdf-pick-container");
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsInitializingPdfPicker = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    [JSInvokable]
    public Task OnPdfClick(int pageNumber, double pdfX, double pdfY, double cssX, double cssY)
    {
        PickedPageNumber = pageNumber;
        PickedPdfX = pdfX;
        PickedPdfY = pdfY;
        return Task.CompletedTask;
    }

    private async Task ApproveSelectedDocumentAsync()
    {
        try
        {
            if (ApprovalReviewDocument?.Document?.Id == Guid.Empty)
            {
                return;
            }

            if (PickedPageNumber <= 0)
            {
                await UiMessageService.Warn(L["PleaseSelectNotePositionOnPdf"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (string.IsNullOrWhiteSpace(ApprovalActionNote))
            {
                await UiMessageService.Warn(L["PleaseEnterApprovalNoteBeforeApproving"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await DocumentsAppService.ApproveWithNoteAsync(new ApproveDocumentWithNoteInput
            {
                DocumentId = ApprovalReviewDocument.Document.Id,
                PageNumber = PickedPageNumber,
                PdfX = PickedPdfX,
                PdfY = PickedPdfY,
                NoteContent = ApprovalActionNote.Trim()
            });

            await UiMessageService.Success(L["DocumentApprovedSuccessfully"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await CloseApprovalReviewModalAsync();
            await GetDocumentsAsync();
            await InvokeAsync(StateHasChanged);
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

    private async Task RejectSelectedDocumentAsync()
    {
        try
        {
            if (ApprovalReviewDocument?.Document?.Id == Guid.Empty)
            {
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            await DocumentsAppService.RejectApprovalAsync(new RejectDocumentApprovalInput
            {
                DocumentId = ApprovalReviewDocument.Document.Id,
                Reason = string.IsNullOrWhiteSpace(ApprovalActionNote)
                    ? L["ApprovalLeaderRejectedDocument"]
                    : ApprovalActionNote.Trim()
            });

            await UiMessageService.Success(L["DocumentRejectedSuccessfully"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await CloseApprovalReviewModalAsync();
            await GetDocumentsAsync();
            await InvokeAsync(StateHasChanged);
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

    #endregion Approval Review

    #region Delete or Revoke Document

    private async Task ShowDeleteOrRevokeModalAsync(DocumentWithNavigationPropertiesDto document)
    {
        if (!CanDeleteOrRevokeInArchiveView)
        {
            await UiMessageService.Warn(L["DeleteRevokeOnlyInArchiveView"], options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            return;
        }

        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);  
            DocumentToDeleteOrRevoke = document;
            DeleteOrRevokeOption = "delete"; // Default option
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
            await InvokeAsync(DeleteOrRevokeModal.Show);
        }
    }

    private async Task CloseDeleteOrRevokeModalAsync()
    {
        await InvokeAsync(DeleteOrRevokeModal.Hide);
        DocumentToDeleteOrRevoke = new();
        DeleteOrRevokeOption = "delete";
    }

    private async Task ConfirmDeleteOrRevokeAsync()
    {
        try
        {
            if (!CanDeleteOrRevokeInArchiveView)
            {
                await UiMessageService.Warn(L["DeleteRevokeOnlyInArchiveView"]);
                await CloseDeleteOrRevokeModalAsync();
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            if (DocumentToDeleteOrRevoke == null || DocumentToDeleteOrRevoke.Document == null)
            {
                await UiMessageService.Error(L["DocumentNotFound"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                return;
            }

            if (DeleteOrRevokeOption == "delete")
            {
                // Delete document - keep existing logic
                await DocumentsAppService.DeleteAsync(DocumentToDeleteOrRevoke.Document.Id);
                await UiMessageService.Success(L["DocumentDeletedSuccessfully"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            }
            else if (DeleteOrRevokeOption == "revoke")
            {
                // Revoke document - new logic
                var revokeInput = new RevokeDocumentInput
                {
                    DocumentId = DocumentToDeleteOrRevoke.Document.Id
                };
                
                var result = await DocumentsAppService.RevokeDocumentAsync(revokeInput);
                
                if (result)
                {
                    await UiMessageService.Success(L["DocumentRevokedSuccessfully"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                }
                else
                {
                        await UiMessageService.Error(L["FailedToRevokeDocument"],
                        options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                    return;
                }
            }

            await GetDocumentsAsync();
            await InvokeAsync(DeleteOrRevokeModal.Hide);
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

    #endregion Delete or Revoke Document

    #region PDF Viewer

    /// <summary>
    /// Open PDF viewer modal for a document. Works for both sourceType 0 (Archive) and 1 (Personal).
    /// Gets DocumentFiles by DocumentId, finds first PDF and displays it.
    /// </summary>
    private async Task OpenDocumentPdfViewerModalAsync(DocumentWithNavigationPropertiesDto context)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);
            if (context?.Document == null)
            {
                await BlockUiService.UnBlock();
                return;
            }

            CurrentDocumentPdfDocumentId = context.Document.Id;
            var pdfFileUrl = await LoadPdfDataUrlAsync(context.Document.Id);
            if (string.IsNullOrEmpty(pdfFileUrl))
            {
                await UiMessageService.Warn(L["NoPdfAvailable"],
                    options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await BlockUiService.UnBlock();
                return;
            }

            DocumentPdfFileUrl = pdfFileUrl;
            IsDocumentPdfFile = true;
            await LoadPreviewDocumentHistoriesAsync(context.Document.Id);
            await DocumentPdfViewerModal.Show();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error opening PDF viewer for document {DocumentId}", context?.Document?.Id);
            await UiMessageService.Warn(L["NoPdfAvailable"] + ": " + ex.Message,
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private async Task CloseDocumentPdfViewerModalAsync()
    {
        if (DocumentPdfViewerModal != null)
        {
            await DocumentPdfViewerModal.Hide();
        }
        DocumentPdfFileUrl = null;
        IsDocumentPdfFile = false;
        CurrentDocumentPdfDocumentId = null;
        PreviewDocumentHistories = new List<DocumentHistoryWithNavigationPropertiesDto>();
    }

    private async Task AssignTaskFromDocumentPdfViewerAsync()
    {
        if (!CurrentDocumentPdfDocumentId.HasValue)
        {
            return;
        }

        var documentId = CurrentDocumentPdfDocumentId.Value;
        await CloseDocumentPdfViewerModalAsync();
        await CreateTaskModalRef.OpenCreateProjectTaskModalAsync(documentId);
    }

    private Task OnTaskCreatedFromPdfAsync()
    {
        return GetDocumentsAsync();
    }

    private async Task<string?> LoadPdfDataUrlAsync(Guid documentId)
    {
        var documentFilesResult = await DocumentFilesAppService.GetListAsync(new GetDocumentFilesInput
        {
            DocumentId = documentId,
            MaxResultCount = 100,
            SkipCount = 0
        });

        var pdfFile = documentFilesResult.Items
            .FirstOrDefault(f => f.DocumentFile != null
                && !string.IsNullOrEmpty(f.DocumentFile.Path)
                && HC.Blazor.Shared.FileHelper.IsPdfFileExtension(f.DocumentFile.Name));

        var pdfFilePath = pdfFile?.DocumentFile?.Path;
        if (string.IsNullOrEmpty(pdfFilePath))
        {
            return null;
        }

        var fileBytes = await DocumentPdfViewerAppService.GetWatermarkedPdfAsync(new HC.DocumentPdfViewer.GetWatermarkedPdfInput
        {
            BlobPath = pdfFilePath,
            WatermarkAction = "view"
        });

        var base64 = Convert.ToBase64String(fileBytes);
        return $"data:application/pdf;base64,{base64}";
    }

    private async Task LoadPreviewDocumentHistoriesAsync(Guid documentId)
    {
        var result = await DocumentHistoriesAppService.GetHistoryByDocumentIdAsync(new GetDocumentHistoriesInput
        {
            DocumentId = documentId,
            SkipCount = 0,
            MaxResultCount = 20
        });

        PreviewDocumentHistories = result.Items;
    }

    private string GetLocalizedHistoryAction(string? action)
    {
        if (string.IsNullOrWhiteSpace(action))
        {
            return string.Empty;
        }

        var localized = L[action];
        return string.Equals(localized.Value, action, StringComparison.Ordinal)
            ? action.Replace("_", " ")
            : localized.Value;
    }

    private string GetHistoryUserDisplayName(IdentityUserDto? user)
    {
        if (user == null)
        {
            return "—";
        }

        var fullName = $"{user.Surname} {user.Name}".Trim();
        return string.IsNullOrWhiteSpace(fullName)
            ? (user.UserName ?? "—")
            : fullName;
    }

    /// <summary>
    /// Build full display name for the currently logged-in user.
    /// ICurrentUser.Name returns only the given name claim, so we combine SurName + Name
    /// to avoid losing the surname in fallback UI (e.g. Submit Approval modal).
    /// </summary>
    private string GetCurrentUserFullName()
    {
        var surname = CurrentUser?.SurName ?? string.Empty;
        var name = CurrentUser?.Name ?? string.Empty;
        var fullName = $"{surname} {name}".Trim();
        if (!string.IsNullOrWhiteSpace(fullName))
        {
            return fullName;
        }

        return CurrentUser?.UserName ?? "—";
    }

    #endregion PDF Viewer

    /// <summary>
    /// Cleanup event handlers when component is disposed
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        SearchDebounceCts?.Cancel();
        SearchDebounceCts?.Dispose();
        DotNetRef?.Dispose();
        NavigationManager.LocationChanged -= OnLocationChanged;
        base.Dispose(disposing);
    }
}
