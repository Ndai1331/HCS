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
using HC.DocumentAssignments;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class DocumentAssignments
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentAssignmentWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<DocumentAssignmentWithNavigationPropertiesDto> DocumentAssignmentList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateDocumentAssignment { get; set; }

    private bool CanEditDocumentAssignment { get; set; }

    private bool CanDeleteDocumentAssignment { get; set; }

    private DocumentAssignmentCreateDto NewDocumentAssignment { get; set; }

    private Validations NewDocumentAssignmentValidations { get; set; } = new();
    private DocumentAssignmentUpdateDto EditingDocumentAssignment { get; set; }

    private Validations EditingDocumentAssignmentValidations { get; set; } = new();
    private Guid EditingDocumentAssignmentId { get; set; }

    private Modal CreateDocumentAssignmentModal { get; set; } = new();
    private Modal EditDocumentAssignmentModal { get; set; } = new();
    private GetDocumentAssignmentsInput Filter { get; set; }

    private DataGridEntityActionsColumn<DocumentAssignmentWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "documentAssignment-create-tab";
    protected string SelectedEditTab = "documentAssignment-edit-tab";
    private DocumentAssignmentWithNavigationPropertiesDto? SelectedDocumentAssignment;

    private IReadOnlyList<LookupDto<Guid>> DocumentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> WorkflowStepTemplatesCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<DocumentAssignmentWithNavigationPropertiesDto> SelectedDocumentAssignments { get; set; } = new();
    private bool AllDocumentAssignmentsSelected { get; set; }

    public DocumentAssignments()
    {
        NewDocumentAssignment = new DocumentAssignmentCreateDto();
        EditingDocumentAssignment = new DocumentAssignmentUpdateDto();
        Filter = new GetDocumentAssignmentsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        DocumentAssignmentList = new List<DocumentAssignmentWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        // Set ReceiverUserId filter to current user
        Filter.ReceiverUserId = CurrentUser.Id;
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentAssignments"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewDocumentAssignment"], async () => {
            await OpenCreateDocumentAssignmentModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.DocumentAssignments.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(DocumentAssignmentWithNavigationPropertiesDto documentAssignment)
    {
        DataGridRef.ToggleDetailRow(documentAssignment, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentAssignmentWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocumentAssignment;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<DocumentAssignmentWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDocumentAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentAssignments.Create);
        CanEditDocumentAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentAssignments.Edit);
        CanDeleteDocumentAssignment = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentAssignments.Delete);
    }

    private async Task GetDocumentAssignmentsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        Filter.ReceiverUserId = CurrentUser.Id; // Ensure filter is set to current user
        var result = await DocumentAssignmentsAppService.GetListAsync(Filter);
        DocumentAssignmentList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentAssignmentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await DocumentAssignmentsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/document-assignments/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&StepOrderMin={Filter.StepOrderMin}&StepOrderMax={Filter.StepOrderMax}&ActionType={HttpUtility.UrlEncode(Filter.ActionType)}&Status={HttpUtility.UrlEncode(Filter.Status)}&AssignedAtMin={Filter.AssignedAtMin?.ToString("O")}&AssignedAtMax={Filter.AssignedAtMax?.ToString("O")}&ProcessedAtMin={Filter.ProcessedAtMin?.ToString("O")}&ProcessedAtMax={Filter.ProcessedAtMax?.ToString("O")}&IsCurrent={Filter.IsCurrent}&DocumentId={Filter.DocumentId}&StepId={Filter.StepId}&ReceiverUserId={Filter.ReceiverUserId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentAssignmentWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentAssignmentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateDocumentAssignmentModalAsync()
    {
        NewDocumentAssignment = new DocumentAssignmentCreateDto
        {
            AssignedAt = DateTime.Now,
            ProcessedAt = DateTime.Now,
        };
        SelectedCreateTab = "documentAssignment-create-tab";
        await NewDocumentAssignmentValidations.ClearAll();
        await CreateDocumentAssignmentModal.Show();
    }

    private async Task CloseCreateDocumentAssignmentModalAsync()
    {
        NewDocumentAssignment = new DocumentAssignmentCreateDto
        {
            AssignedAt = DateTime.Now,
            ProcessedAt = DateTime.Now,
        };
        await CreateDocumentAssignmentModal.Hide();
    }

    private async Task OpenEditDocumentAssignmentModalAsync(DocumentAssignmentWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "documentAssignment-edit-tab";
        var documentAssignment = await DocumentAssignmentsAppService.GetWithNavigationPropertiesAsync(input.DocumentAssignment.Id);
        EditingDocumentAssignmentId = documentAssignment.DocumentAssignment.Id;
        EditingDocumentAssignment = ObjectMapper.Map<DocumentAssignmentDto, DocumentAssignmentUpdateDto>(documentAssignment.DocumentAssignment);
        await EditingDocumentAssignmentValidations.ClearAll();
        await EditDocumentAssignmentModal.Show();
    }

    private async Task DeleteDocumentAssignmentAsync(DocumentAssignmentWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentAssignmentsAppService.DeleteAsync(input.DocumentAssignment.Id);
            await GetDocumentAssignmentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentAssignmentWithConfirmationAsync(DocumentAssignmentWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteDocumentAssignmentAsync(input);
        }
    }

    private async Task CreateDocumentAssignmentAsync()
    {
        try
        {
            if (await NewDocumentAssignmentValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentAssignmentsAppService.CreateAsync(NewDocumentAssignment);
            await GetDocumentAssignmentsAsync();
            await CloseCreateDocumentAssignmentModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditDocumentAssignmentModalAsync()
    {
        await EditDocumentAssignmentModal.Hide();
    }

    private async Task UpdateDocumentAssignmentAsync()
    {
        try
        {
            if (await EditingDocumentAssignmentValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentAssignmentsAppService.UpdateAsync(EditingDocumentAssignmentId, EditingDocumentAssignment);
            await GetDocumentAssignmentsAsync();
            await EditDocumentAssignmentModal.Hide();
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

    protected virtual async Task OnStepOrderMinChangedAsync(int? stepOrderMin)
    {
        Filter.StepOrderMin = stepOrderMin;
        await SearchAsync();
    }

    protected virtual async Task OnStepOrderMaxChangedAsync(int? stepOrderMax)
    {
        Filter.StepOrderMax = stepOrderMax;
        await SearchAsync();
    }

    protected virtual async Task OnActionTypeChangedAsync(string? actionType)
    {
        Filter.ActionType = actionType;
        await SearchAsync();
    }

    protected virtual async Task OnStatusChangedAsync(string? status)
    {
        Filter.Status = status;
        await SearchAsync();
    }

    protected virtual async Task OnAssignedAtMinChangedAsync(DateTime? assignedAtMin)
    {
        Filter.AssignedAtMin = assignedAtMin.HasValue ? assignedAtMin.Value.Date : assignedAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnAssignedAtMaxChangedAsync(DateTime? assignedAtMax)
    {
        Filter.AssignedAtMax = assignedAtMax.HasValue ? assignedAtMax.Value.Date.AddDays(1).AddSeconds(-1) : assignedAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnProcessedAtMinChangedAsync(DateTime? processedAtMin)
    {
        Filter.ProcessedAtMin = processedAtMin.HasValue ? processedAtMin.Value.Date : processedAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnProcessedAtMaxChangedAsync(DateTime? processedAtMax)
    {
        Filter.ProcessedAtMax = processedAtMax.HasValue ? processedAtMax.Value.Date.AddDays(1).AddSeconds(-1) : processedAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnIsCurrentChangedAsync(bool? isCurrent)
    {
        Filter.IsCurrent = isCurrent;
        await SearchAsync();
    }

    protected virtual async Task OnDocumentIdChangedAsync(Guid? documentId)
    {
        Filter.DocumentId = documentId;
        await SearchAsync();
    }

    protected virtual async Task OnStepIdChangedAsync(Guid? stepId)
    {
        Filter.StepId = stepId;
        await SearchAsync();
    }

    protected virtual async Task OnReceiverUserIdChangedAsync(Guid? receiverUserId)
    {
        Filter.ReceiverUserId = receiverUserId;
        await SearchAsync();
    }

    private async Task GetDocumentCollectionLookupAsync(string? newValue = null)
    {
        DocumentsCollection = (await DocumentAssignmentsAppService.GetDocumentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetWorkflowStepTemplateCollectionLookupAsync(string? newValue = null)
    {
        WorkflowStepTemplatesCollection = (await DocumentAssignmentsAppService.GetWorkflowStepTemplateLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await DocumentAssignmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllDocumentAssignmentsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllDocumentAssignmentsSelected = false;
        SelectedDocumentAssignments.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedDocumentAssignmentRowsChanged()
    {
        if (SelectedDocumentAssignments.Count != PageSize)
        {
            AllDocumentAssignmentsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedDocumentAssignmentsAsync()
    {
        var message = AllDocumentAssignmentsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedDocumentAssignments.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllDocumentAssignmentsSelected)
        {
            await DocumentAssignmentsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await DocumentAssignmentsAppService.DeleteByIdsAsync(SelectedDocumentAssignments.Select(x => x.DocumentAssignment.Id).ToList());
        }

        SelectedDocumentAssignments.Clear();
        AllDocumentAssignmentsSelected = false;
        await GetDocumentAssignmentsAsync();
    }

    private void ViewDocumentAsync(DocumentAssignmentWithNavigationPropertiesDto input)
    {
        NavigationManager.NavigateTo($"/view-document-detail/{input.DocumentAssignment.DocumentId}");
    }
}