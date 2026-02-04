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
using HC.Departments;
using System.Threading;
using Excubo.Blazor.TreeViews;
using Microsoft.AspNetCore.Components.Web;
using HC.MasterDatas;
using Volo.Abp.AspNetCore.Components.Messages;

namespace HC.Blazor.Pages.Documents;

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
    private bool CanSendDocument { get; set; }
    private bool CanSubmitForSigning { get; set; }

    private GetDocumentsInput Filter { get; set; } = new GetDocumentsInput();
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

    private Modal SendDocumentModal { get; set; } = new();


    public Documents()
    {
        DepartmentList = new List<DepartmentDto>();
        DepartmentTreeViews = new List<DepartmentTreeView>();
        AllDepartmentsFlat = new List<DepartmentTreeView>();
        AllDepartmentsForSelect2 = new List<DepartmentTreeView>();
    }
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Logger.LogInformation("Documents OnAfterRenderAsync start");

        if (firstRender)
        {
            Logger.LogInformation("Documents OnAfterRenderAsync firstRender");
            
            // Check for sourceType query parameter
            var uri = NavigationManager.ToAbsoluteUri(NavigationManager.Uri);
            var query = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);
            if (query.TryGetValue("sourceType", out var sourceTypeValue) && int.TryParse(sourceTypeValue, out var sourceTypeInt))
            {
                SelectedSourceType = (DocumentSourceType)sourceTypeInt;
                Filter.SourceType = SelectedSourceType;
                
                // For Personal documents, also filter by current user
                if (SelectedSourceType == DocumentSourceType.Personal)
                {
                    Filter.CreatorId = CurrentUser.Id;
                }
            }
            
            await SetPermissionsAsync();
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await GetDepartmentsAsync();
            await GetStatusMasterDataLookupAsync(StatusMasterDataCollection, string.Empty, CancellationToken.None);
            await GetTypeMasterDataLookupAsync(TypeMasterDataCollection, "", CancellationToken.None);
            await GetUrgencyLevelMasterDataLookupAsync(UrgencyLevelMasterDataCollection, "", CancellationToken.None);
            await GetSecrecyLevelMasterDataLookupAsync(SecrecyLevelMasterDataCollection, "", CancellationToken.None);
            await GetFieldMasterDataLookupAsync(FieldMasterDataCollection, "", CancellationToken.None);
            await GetStatusMasterDataLookupAsync(StatusMasterDataCollection, "", CancellationToken.None);
            await GetUnitLookupAsync(UnitsCollection, "", CancellationToken.None);
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
            Toolbar.AddButton(L["NewDocument"], () => {
                var sourceTypeParam = SelectedSourceType.HasValue ? $"?sourceType={(int)SelectedSourceType.Value}" : "";
                NavigationManager.NavigateTo("/document-detail" + sourceTypeParam);
                return Task.CompletedTask;
            }, IconName.Add, requiredPolicyName: HCPermissions.Documents.Create);
        }

        return ValueTask.CompletedTask;
    }
    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocument;

    private async Task SetPermissionsAsync()
    {
        CanCreateDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Create);
        CanEditDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Edit);
        CanSendDocument = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.Send);
        CanSubmitForSigning = await AuthorizationService.IsGrantedAsync(HCPermissions.Documents.SubmitForSigning);
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
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/documents/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&No={HttpUtility.UrlEncode(Filter.No)}&Title={HttpUtility.UrlEncode(Filter.Title)}&CurrentStatus={HttpUtility.UrlEncode(Filter.CurrentStatus)}&CompletedTimeMin={Filter.CompletedTimeMin?.ToString("O")}&CompletedTimeMax={Filter.CompletedTimeMax?.ToString("O")}&StorageNumber={HttpUtility.UrlEncode(Filter.StorageNumber)}&IncommingDateMin={Filter.IncommingDateMin?.ToString("O")}&IncommingDateMax={Filter.IncommingDateMax?.ToString("O")}&FieldId={Filter.FieldId}&UnitId={Filter.UnitId}&WorkflowId={Filter.WorkflowId}&StatusId={Filter.StatusId}&TypeId={Filter.TypeId}&UrgencyLevelId={Filter.UrgencyLevelId}&SecrecyLevelId={Filter.SecrecyLevelId}&SourceType={Filter.SourceType}&CreatorId={Filter.CreatorId}", forceLoad: true);
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

    protected virtual async Task OnSourceTypeChangedAsync(DocumentSourceType? sourceType)
    {
        Filter.SourceType = sourceType;
        await SearchAsync();
    }

    private async Task<List<LookupDto<Guid>>> GetTypeMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.DocumentType.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        TypeMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return TypeMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetUrgencyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.UrgencyLevel.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        UrgencyLevelMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return UrgencyLevelMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetSecrecyLevelMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.SecrecyLevel.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        SecrecyLevelMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return SecrecyLevelMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetFieldMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.Field.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        FieldMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return FieldMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetStatusMasterDataLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await MasterDatasAppService.GetListAsync(new GetMasterDatasInput
        {
            Type = MasterDataType.Status.GetTypeValue(),
            FilterText = filter,
            MaxResultCount = 1000,
            SkipCount = 0
        });
        StatusMasterDataCollection = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
        return StatusMasterDataCollection.ToList();
    }

    private async Task<List<LookupDto<Guid>>> GetUnitLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        var result = await DocumentsAppService.GetUnitLookupAsync(new LookupRequestDto { Filter = filter });
        UnitsCollection = result.Items;
        return UnitsCollection.ToList();
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
    private List<LookupDto<Guid>> SelectedRecipients { get; set; } = new();
    private List<DepartmentTreeView> SelectedDepartments { get; set; } = new();
    private DepartmentTreeView SelectedItem { get; set; } = new();

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

    private async Task CloseSendDocumentModalAsync()
    {
        await InvokeAsync(SendDocumentModal.Hide);
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

    #region Delete or Revoke Document

    private async Task ShowDeleteOrRevokeModalAsync(DocumentWithNavigationPropertiesDto document)
    {
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
}