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
using HC.DocumentWorkflowInstances;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;

namespace HC.Blazor.Pages;

public partial class DocumentWorkflowInstances
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentWorkflowInstanceWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<DocumentWorkflowInstanceWithNavigationPropertiesDto> DocumentWorkflowInstanceList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateDocumentWorkflowInstance { get; set; }

    private bool CanEditDocumentWorkflowInstance { get; set; }

    private bool CanDeleteDocumentWorkflowInstance { get; set; }

    private DocumentWorkflowInstanceCreateDto NewDocumentWorkflowInstance { get; set; }

    private Validations NewDocumentWorkflowInstanceValidations { get; set; } = new();
    private DocumentWorkflowInstanceUpdateDto EditingDocumentWorkflowInstance { get; set; }

    private Validations EditingDocumentWorkflowInstanceValidations { get; set; } = new();
    private Guid EditingDocumentWorkflowInstanceId { get; set; }

    private Modal CreateDocumentWorkflowInstanceModal { get; set; } = new();
    private Modal EditDocumentWorkflowInstanceModal { get; set; } = new();
    private GetDocumentWorkflowInstancesInput Filter { get; set; }

    private DataGridEntityActionsColumn<DocumentWorkflowInstanceWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "documentWorkflowInstance-create-tab";
    protected string SelectedEditTab = "documentWorkflowInstance-edit-tab";
    private DocumentWorkflowInstanceWithNavigationPropertiesDto? SelectedDocumentWorkflowInstance;

    private IReadOnlyList<LookupDto<Guid>> DocumentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowTemplatesCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowStepTemplatesCollection { get; set; } = new List<LookupDto<Guid>>();
    #region Child Entities
    #region DocumentWorkflowInstanceFiles
    private bool CanListDocumentWorkflowInstanceFile { get; set; }

    private bool CanCreateDocumentWorkflowInstanceFile { get; set; }

    private bool CanEditDocumentWorkflowInstanceFile { get; set; }

    private bool CanDeleteDocumentWorkflowInstanceFile { get; set; }

    private DocumentWorkflowInstanceFileCreateDto NewDocumentWorkflowInstanceFile { get; set; }

    private Dictionary<Guid, DataGrid<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> DocumentWorkflowInstanceFileDataGrids { get; set; } = new();
    private int DocumentWorkflowInstanceFilePageSize { get; } = 5;
    private DataGridEntityActionsColumn<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> DocumentWorkflowInstanceFileEntityActionsColumns { get; set; } = new();
    private Validations NewDocumentWorkflowInstanceFileValidations { get; set; } = new();
    private Modal CreateDocumentWorkflowInstanceFileModal { get; set; } = new();
    private Guid EditingDocumentWorkflowInstanceFileId { get; set; }

    private DocumentWorkflowInstanceFileUpdateDto EditingDocumentWorkflowInstanceFile { get; set; }

    private Validations EditingDocumentWorkflowInstanceFileValidations { get; set; } = new();
    private Modal EditDocumentWorkflowInstanceFileModal { get; set; } = new();
    private IReadOnlyList<LookupDto<Guid>> DocumentFilesCollection { get; set; } = new List<LookupDto<Guid>>();
    #endregion
    #region DocumentWorkflowInstanceLogss
    private bool CanListDocumentWorkflowInstanceLogs { get; set; }

    private bool CanCreateDocumentWorkflowInstanceLogs { get; set; }

    private bool CanEditDocumentWorkflowInstanceLogs { get; set; }

    private bool CanDeleteDocumentWorkflowInstanceLogs { get; set; }

    private DocumentWorkflowInstanceLogsCreateDto NewDocumentWorkflowInstanceLogs { get; set; }

    private Dictionary<Guid, DataGrid<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> DocumentWorkflowInstanceLogsDataGrids { get; set; } = new();
    private int DocumentWorkflowInstanceLogsPageSize { get; } = 5;
    private DataGridEntityActionsColumn<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> DocumentWorkflowInstanceLogsEntityActionsColumns { get; set; } = new();
    private Validations NewDocumentWorkflowInstanceLogsValidations { get; set; } = new();
    private Modal CreateDocumentWorkflowInstanceLogsModal { get; set; } = new();
    private Guid EditingDocumentWorkflowInstanceLogsId { get; set; }

    private DocumentWorkflowInstanceLogsUpdateDto EditingDocumentWorkflowInstanceLogs { get; set; }

    private Validations EditingDocumentWorkflowInstanceLogsValidations { get; set; } = new();
    private Modal EditDocumentWorkflowInstanceLogsModal { get; set; } = new();
    private IReadOnlyList<LookupDto<Guid>> DocumentAssignmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    #endregion
    #endregion
    private List<DocumentWorkflowInstanceWithNavigationPropertiesDto> SelectedDocumentWorkflowInstances { get; set; } = new();
    private bool AllDocumentWorkflowInstancesSelected { get; set; }

    public DocumentWorkflowInstances()
    {
        NewDocumentWorkflowInstance = new DocumentWorkflowInstanceCreateDto();
        EditingDocumentWorkflowInstance = new DocumentWorkflowInstanceUpdateDto();
        Filter = new GetDocumentWorkflowInstancesInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        DocumentWorkflowInstanceList = new List<DocumentWorkflowInstanceWithNavigationPropertiesDto>();
        NewDocumentWorkflowInstanceFile = new DocumentWorkflowInstanceFileCreateDto();
        EditingDocumentWorkflowInstanceFile = new DocumentWorkflowInstanceFileUpdateDto();
        NewDocumentWorkflowInstanceLogs = new DocumentWorkflowInstanceLogsCreateDto();
        EditingDocumentWorkflowInstanceLogs = new DocumentWorkflowInstanceLogsUpdateDto();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetWorkflowTemplateCollectionLookupAsync();
        await GetDocumentFileLookupAsync();
        await GetDocumentAssignmentLookupAsync();
        await GetIdentityUserLookupAsync();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentWorkflowInstances"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewDocumentWorkflowInstance"], async () => {
            await OpenCreateDocumentWorkflowInstanceModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.DocumentWorkflowInstances.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(DocumentWorkflowInstanceWithNavigationPropertiesDto documentWorkflowInstance)
    {
        // Toggle expanded row tracking for chevron icon display
        if (SelectedDocumentWorkflowInstance?.DocumentWorkflowInstance.Id == documentWorkflowInstance.DocumentWorkflowInstance.Id)
        {
            SelectedDocumentWorkflowInstance = null;
        }
        else
        {
            SelectedDocumentWorkflowInstance = documentWorkflowInstance;
        }
        DataGridRef.ToggleDetailRow(documentWorkflowInstance, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentWorkflowInstanceWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocumentWorkflowInstance;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<DocumentWorkflowInstanceWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDocumentWorkflowInstance = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstances.Create);
        CanEditDocumentWorkflowInstance = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstances.Edit);
        CanDeleteDocumentWorkflowInstance = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstances.Delete);
        #region DocumentWorkflowInstanceFiles
        CanListDocumentWorkflowInstanceFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceFiles.Default);
        CanCreateDocumentWorkflowInstanceFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceFiles.Create);
        CanEditDocumentWorkflowInstanceFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceFiles.Edit);
        CanDeleteDocumentWorkflowInstanceFile = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceFiles.Delete);
        #endregion
        #region DocumentWorkflowInstanceLogss
        CanListDocumentWorkflowInstanceLogs = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceLogss.Default);
        CanCreateDocumentWorkflowInstanceLogs = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceLogss.Create);
        CanEditDocumentWorkflowInstanceLogs = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceLogss.Edit);
        CanDeleteDocumentWorkflowInstanceLogs = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentWorkflowInstanceLogss.Delete);
        #endregion
    }

    private async Task GetDocumentWorkflowInstancesAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await DocumentWorkflowInstancesAppService.GetListAsync(Filter);
        DocumentWorkflowInstanceList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentWorkflowInstancesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await DocumentWorkflowInstancesAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/document-workflow-instances/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Status={HttpUtility.UrlEncode(Filter.Status)}&StartedAtMin={Filter.StartedAtMin?.ToString("O")}&StartedAtMax={Filter.StartedAtMax?.ToString("O")}&FinishedAtMin={Filter.FinishedAtMin?.ToString("O")}&FinishedAtMax={Filter.FinishedAtMax?.ToString("O")}&DocumentId={Filter.DocumentId}&WorkflowId={Filter.WorkflowId}&WorkflowTemplateId={Filter.WorkflowTemplateId}&CurrentStepId={Filter.CurrentStepId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentWorkflowInstanceWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentWorkflowInstancesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateDocumentWorkflowInstanceModalAsync()
    {
        NewDocumentWorkflowInstance = new DocumentWorkflowInstanceCreateDto
        {
            StartedAt = DateTime.Now,
            FinishedAt = DateTime.Now,
            WorkflowTemplateId = WorkflowTemplatesCollection.Select(i => i.Id).FirstOrDefault(),
        };
        SelectedCreateTab = "documentWorkflowInstance-create-tab";
        await NewDocumentWorkflowInstanceValidations.ClearAll();
        await CreateDocumentWorkflowInstanceModal.Show();
    }

    private async Task CloseCreateDocumentWorkflowInstanceModalAsync()
    {
        NewDocumentWorkflowInstance = new DocumentWorkflowInstanceCreateDto
        {
            StartedAt = DateTime.Now,
            FinishedAt = DateTime.Now,
            WorkflowTemplateId = WorkflowTemplatesCollection.Select(i => i.Id).FirstOrDefault(),
        };
        await CreateDocumentWorkflowInstanceModal.Hide();
    }

    private async Task OpenEditDocumentWorkflowInstanceModalAsync(DocumentWorkflowInstanceWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "documentWorkflowInstance-edit-tab";
        var documentWorkflowInstance = await DocumentWorkflowInstancesAppService.GetWithNavigationPropertiesAsync(input.DocumentWorkflowInstance.Id);
        EditingDocumentWorkflowInstanceId = documentWorkflowInstance.DocumentWorkflowInstance.Id;
        EditingDocumentWorkflowInstance = ObjectMapper.Map<DocumentWorkflowInstanceDto, DocumentWorkflowInstanceUpdateDto>(documentWorkflowInstance.DocumentWorkflowInstance);
        await EditingDocumentWorkflowInstanceValidations.ClearAll();
        await EditDocumentWorkflowInstanceModal.Show();
    }

    private async Task DeleteDocumentWorkflowInstanceAsync(DocumentWorkflowInstanceWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentWorkflowInstancesAppService.DeleteAsync(input.DocumentWorkflowInstance.Id);
            await GetDocumentWorkflowInstancesAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CreateDocumentWorkflowInstanceAsync()
    {
        try
        {
            if (await NewDocumentWorkflowInstanceValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstancesAppService.CreateAsync(NewDocumentWorkflowInstance);
            await GetDocumentWorkflowInstancesAsync();
            await CloseCreateDocumentWorkflowInstanceModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditDocumentWorkflowInstanceModalAsync()
    {
        await EditDocumentWorkflowInstanceModal.Hide();
    }

    private async Task UpdateDocumentWorkflowInstanceAsync()
    {
        try
        {
            if (await EditingDocumentWorkflowInstanceValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstancesAppService.UpdateAsync(EditingDocumentWorkflowInstanceId, EditingDocumentWorkflowInstance);
            await GetDocumentWorkflowInstancesAsync();
            await EditDocumentWorkflowInstanceModal.Hide();
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

    protected virtual async Task OnStatusChangedAsync(string? status)
    {
        Filter.Status = status;
        await SearchAsync();
    }

    protected virtual async Task OnStartedAtMinChangedAsync(DateTime? startedAtMin)
    {
        Filter.StartedAtMin = startedAtMin.HasValue ? startedAtMin.Value.Date : startedAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnStartedAtMaxChangedAsync(DateTime? startedAtMax)
    {
        Filter.StartedAtMax = startedAtMax.HasValue ? startedAtMax.Value.Date.AddDays(1).AddSeconds(-1) : startedAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnFinishedAtMinChangedAsync(DateTime? finishedAtMin)
    {
        Filter.FinishedAtMin = finishedAtMin.HasValue ? finishedAtMin.Value.Date : finishedAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnFinishedAtMaxChangedAsync(DateTime? finishedAtMax)
    {
        Filter.FinishedAtMax = finishedAtMax.HasValue ? finishedAtMax.Value.Date.AddDays(1).AddSeconds(-1) : finishedAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnDocumentIdChangedAsync(Guid? documentId)
    {
        Filter.DocumentId = documentId;
        await SearchAsync();
    }

    protected virtual async Task OnWorkflowIdChangedAsync(Guid? workflowId)
    {
        Filter.WorkflowId = workflowId;
        await SearchAsync();
    }

    protected virtual async Task OnWorkflowTemplateIdChangedAsync(Guid? workflowTemplateId)
    {
        Filter.WorkflowTemplateId = workflowTemplateId;
        await SearchAsync();
    }

    protected virtual async Task OnCurrentStepIdChangedAsync(Guid? currentStepId)
    {
        Filter.CurrentStepId = currentStepId;
        await SearchAsync();
    }

    private async Task GetDocumentCollectionLookupAsync(string? newValue = null)
    {
        DocumentsCollection = (await DocumentWorkflowInstancesAppService.GetDocumentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetWorkflowCollectionLookupAsync(string? newValue = null)
    {
        WorkflowsCollection = (await DocumentWorkflowInstancesAppService.GetWorkflowLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetWorkflowTemplateCollectionLookupAsync(string? newValue = null)
    {
        WorkflowTemplatesCollection = (await DocumentWorkflowInstancesAppService.GetWorkflowTemplateLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetWorkflowStepTemplateCollectionLookupAsync(string? newValue = null)
    {
        WorkflowStepTemplatesCollection = (await DocumentWorkflowInstancesAppService.GetWorkflowStepTemplateLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private bool ShouldShowDetailRow()
    {
        return CanListDocumentWorkflowInstanceFile || CanListDocumentWorkflowInstanceLogs;
    }

    public string SelectedChildTab { get; set; } = "documentworkflowinstancefile-tab";

    private Task OnSelectedChildTabChanged(string name)
    {
        SelectedChildTab = name;
        return Task.CompletedTask;
    }

    private Task SelectAllItems()
    {
        AllDocumentWorkflowInstancesSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllDocumentWorkflowInstancesSelected = false;
        SelectedDocumentWorkflowInstances.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedDocumentWorkflowInstanceRowsChanged()
    {
        if (SelectedDocumentWorkflowInstances.Count != PageSize)
        {
            AllDocumentWorkflowInstancesSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedDocumentWorkflowInstancesAsync()
    {
        var message = AllDocumentWorkflowInstancesSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedDocumentWorkflowInstances.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllDocumentWorkflowInstancesSelected)
        {
            await DocumentWorkflowInstancesAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await DocumentWorkflowInstancesAppService.DeleteByIdsAsync(SelectedDocumentWorkflowInstances.Select(x => x.DocumentWorkflowInstance.Id).ToList());
        }

        SelectedDocumentWorkflowInstances.Clear();
        AllDocumentWorkflowInstancesSelected = false;
        await GetDocumentWorkflowInstancesAsync();
    }
    #region DocumentWorkflowInstanceFiles
    private async Task OnDocumentWorkflowInstanceFileDataGridReadAsync(DataGridReadDataEventArgs<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> e, Guid documentWorkflowInstanceId)
    {
        var sorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        var currentPage = e.Page;
        await SetDocumentWorkflowInstanceFilesAsync(documentWorkflowInstanceId, currentPage, sorting: sorting);
        await InvokeAsync(StateHasChanged);
    }

    private async Task SetDocumentWorkflowInstanceFilesAsync(Guid documentWorkflowInstanceId, int currentPage = 1, string? sorting = null)
    {
        var documentWorkflowInstance = DocumentWorkflowInstanceList.FirstOrDefault(x => x.DocumentWorkflowInstance.Id == documentWorkflowInstanceId);
        if (documentWorkflowInstance == null)
        {
            return;
        }

        var documentWorkflowInstanceFiles = await DocumentWorkflowInstanceFilesAppService.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(new GetDocumentWorkflowInstanceFileListInput { DocumentWorkflowInstanceId = documentWorkflowInstanceId, MaxResultCount = DocumentWorkflowInstanceFilePageSize, SkipCount = (currentPage - 1) * DocumentWorkflowInstanceFilePageSize, Sorting = sorting });
        documentWorkflowInstance.DocumentWorkflowInstance.DocumentWorkflowInstanceFiles = documentWorkflowInstanceFiles.Items.ToList();
        var documentWorkflowInstanceFileDataGrid = DocumentWorkflowInstanceFileDataGrids[documentWorkflowInstanceId];
        documentWorkflowInstanceFileDataGrid.CurrentPage = currentPage;
        documentWorkflowInstanceFileDataGrid.TotalItems = (int)documentWorkflowInstanceFiles.TotalCount;
    }

    private async Task OpenEditDocumentWorkflowInstanceFileModalAsync(DocumentWorkflowInstanceFileWithNavigationPropertiesDto input)
    {
        var documentWorkflowInstanceFile = await DocumentWorkflowInstanceFilesAppService.GetAsync(input.DocumentWorkflowInstanceFile.Id);
        EditingDocumentWorkflowInstanceFileId = documentWorkflowInstanceFile.Id;
        EditingDocumentWorkflowInstanceFile = ObjectMapper.Map<DocumentWorkflowInstanceFileDto, DocumentWorkflowInstanceFileUpdateDto>(documentWorkflowInstanceFile);
        await EditingDocumentWorkflowInstanceFileValidations.ClearAll();
        await EditDocumentWorkflowInstanceFileModal.Show();
    }

    private async Task CloseEditDocumentWorkflowInstanceFileModalAsync()
    {
        await EditDocumentWorkflowInstanceFileModal.Hide();
    }

    private async Task UpdateDocumentWorkflowInstanceFileAsync()
    {
        try
        {
            if (await EditingDocumentWorkflowInstanceFileValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstanceFilesAppService.UpdateAsync(EditingDocumentWorkflowInstanceFileId, EditingDocumentWorkflowInstanceFile);
            await SetDocumentWorkflowInstanceFilesAsync(EditingDocumentWorkflowInstanceFile.DocumentWorkflowInstanceId);
            await EditDocumentWorkflowInstanceFileModal.Hide();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentWorkflowInstanceFileAsync(DocumentWorkflowInstanceFileWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentWorkflowInstanceFilesAppService.DeleteAsync(input.DocumentWorkflowInstanceFile.Id);
            await SetDocumentWorkflowInstanceFilesAsync(input.DocumentWorkflowInstanceFile.DocumentWorkflowInstanceId);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OpenCreateDocumentWorkflowInstanceFileModalAsync(Guid documentWorkflowInstanceId)
    {
        NewDocumentWorkflowInstanceFile = new DocumentWorkflowInstanceFileCreateDto
        {
            DocumentWorkflowInstanceId = documentWorkflowInstanceId
        };
        await NewDocumentWorkflowInstanceFileValidations.ClearAll();
        await CreateDocumentWorkflowInstanceFileModal.Show();
    }

    private async Task CloseCreateDocumentWorkflowInstanceFileModalAsync()
    {
        NewDocumentWorkflowInstanceFile = new DocumentWorkflowInstanceFileCreateDto();
        await CreateDocumentWorkflowInstanceFileModal.Hide();
    }

    private async Task CreateDocumentWorkflowInstanceFileAsync()
    {
        try
        {
            if (await NewDocumentWorkflowInstanceFileValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstanceFilesAppService.CreateAsync(NewDocumentWorkflowInstanceFile);
            await SetDocumentWorkflowInstanceFilesAsync(NewDocumentWorkflowInstanceFile.DocumentWorkflowInstanceId);
            await CloseCreateDocumentWorkflowInstanceFileModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task GetDocumentFileLookupAsync(string? filter = null)
    {
        DocumentFilesCollection = (await DocumentWorkflowInstanceFilesAppService.GetDocumentFileLookupAsync(new LookupRequestDto { Filter = filter })).Items;
    }
    #endregion
    #region DocumentWorkflowInstanceLogss
    private async Task OnDocumentWorkflowInstanceLogsDataGridReadAsync(DataGridReadDataEventArgs<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> e, Guid documentWorkflowInstanceId)
    {
        var sorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        var currentPage = e.Page;
        await SetDocumentWorkflowInstanceLogssAsync(documentWorkflowInstanceId, currentPage, sorting: sorting);
        await InvokeAsync(StateHasChanged);
    }

    private async Task SetDocumentWorkflowInstanceLogssAsync(Guid documentWorkflowInstanceId, int currentPage = 1, string? sorting = null)
    {
        var documentWorkflowInstance = DocumentWorkflowInstanceList.FirstOrDefault(x => x.DocumentWorkflowInstance.Id == documentWorkflowInstanceId);
        if (documentWorkflowInstance == null)
        {
            return;
        }

        var documentWorkflowInstanceLogss = await DocumentWorkflowInstanceLogssAppService.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(new GetDocumentWorkflowInstanceLogsListInput { DocumentWorkflowInstanceId = documentWorkflowInstanceId, MaxResultCount = DocumentWorkflowInstanceLogsPageSize, SkipCount = (currentPage - 1) * DocumentWorkflowInstanceLogsPageSize, Sorting = sorting });
        documentWorkflowInstance.DocumentWorkflowInstance.DocumentWorkflowInstanceLogss = documentWorkflowInstanceLogss.Items.ToList();
        var documentWorkflowInstanceLogsDataGrid = DocumentWorkflowInstanceLogsDataGrids[documentWorkflowInstanceId];
        documentWorkflowInstanceLogsDataGrid.CurrentPage = currentPage;
        documentWorkflowInstanceLogsDataGrid.TotalItems = (int)documentWorkflowInstanceLogss.TotalCount;
    }

    private async Task OpenEditDocumentWorkflowInstanceLogsModalAsync(DocumentWorkflowInstanceLogsWithNavigationPropertiesDto input)
    {
        var documentWorkflowInstanceLogs = await DocumentWorkflowInstanceLogssAppService.GetAsync(input.DocumentWorkflowInstanceLogs.Id);
        EditingDocumentWorkflowInstanceLogsId = documentWorkflowInstanceLogs.Id;
        EditingDocumentWorkflowInstanceLogs = ObjectMapper.Map<DocumentWorkflowInstanceLogsDto, DocumentWorkflowInstanceLogsUpdateDto>(documentWorkflowInstanceLogs);
        await EditingDocumentWorkflowInstanceLogsValidations.ClearAll();
        await EditDocumentWorkflowInstanceLogsModal.Show();
    }

    private async Task CloseEditDocumentWorkflowInstanceLogsModalAsync()
    {
        await EditDocumentWorkflowInstanceLogsModal.Hide();
    }

    private async Task UpdateDocumentWorkflowInstanceLogsAsync()
    {
        try
        {
            if (await EditingDocumentWorkflowInstanceLogsValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstanceLogssAppService.UpdateAsync(EditingDocumentWorkflowInstanceLogsId, EditingDocumentWorkflowInstanceLogs);
            await SetDocumentWorkflowInstanceLogssAsync(EditingDocumentWorkflowInstanceLogs.DocumentWorkflowInstanceId);
            await EditDocumentWorkflowInstanceLogsModal.Hide();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentWorkflowInstanceLogsAsync(DocumentWorkflowInstanceLogsWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentWorkflowInstanceLogssAppService.DeleteAsync(input.DocumentWorkflowInstanceLogs.Id);
            await SetDocumentWorkflowInstanceLogssAsync(input.DocumentWorkflowInstanceLogs.DocumentWorkflowInstanceId);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task OpenCreateDocumentWorkflowInstanceLogsModalAsync(Guid documentWorkflowInstanceId)
    {
        NewDocumentWorkflowInstanceLogs = new DocumentWorkflowInstanceLogsCreateDto
        {
            DocumentWorkflowInstanceId = documentWorkflowInstanceId
        };
        await NewDocumentWorkflowInstanceLogsValidations.ClearAll();
        await CreateDocumentWorkflowInstanceLogsModal.Show();
    }

    private async Task CloseCreateDocumentWorkflowInstanceLogsModalAsync()
    {
        NewDocumentWorkflowInstanceLogs = new DocumentWorkflowInstanceLogsCreateDto();
        await CreateDocumentWorkflowInstanceLogsModal.Hide();
    }

    private async Task CreateDocumentWorkflowInstanceLogsAsync()
    {
        try
        {
            if (await NewDocumentWorkflowInstanceLogsValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentWorkflowInstanceLogssAppService.CreateAsync(NewDocumentWorkflowInstanceLogs);
            await SetDocumentWorkflowInstanceLogssAsync(NewDocumentWorkflowInstanceLogs.DocumentWorkflowInstanceId);
            await CloseCreateDocumentWorkflowInstanceLogsModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task GetDocumentAssignmentLookupAsync(string? filter = null)
    {
        DocumentAssignmentsCollection = (await DocumentWorkflowInstanceLogssAppService.GetDocumentAssignmentLookupAsync(new LookupRequestDto { Filter = filter })).Items;
    }

    private async Task GetIdentityUserLookupAsync(string? filter = null)
    {
        IdentityUsersCollection = (await DocumentWorkflowInstanceLogssAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = filter })).Items;
    }
    #endregion
}