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
using HC.DocumentHistories;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class DocumentHistories
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentHistoryWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<DocumentHistoryWithNavigationPropertiesDto> DocumentHistoryList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateDocumentHistory { get; set; }

    private bool CanEditDocumentHistory { get; set; }

    private bool CanDeleteDocumentHistory { get; set; }

    private DocumentHistoryCreateDto NewDocumentHistory { get; set; }

    private Validations NewDocumentHistoryValidations { get; set; } = new();
    private DocumentHistoryUpdateDto EditingDocumentHistory { get; set; }

    private Validations EditingDocumentHistoryValidations { get; set; } = new();
    private Guid EditingDocumentHistoryId { get; set; }

    private Modal CreateDocumentHistoryModal { get; set; } = new();
    private Modal EditDocumentHistoryModal { get; set; } = new();
    private GetDocumentHistoriesInput Filter { get; set; }

    private DataGridEntityActionsColumn<DocumentHistoryWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "documentHistory-create-tab";
    protected string SelectedEditTab = "documentHistory-edit-tab";
    private DocumentHistoryWithNavigationPropertiesDto? SelectedDocumentHistory;

    private IReadOnlyList<LookupDto<Guid>> DocumentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<DocumentHistoryWithNavigationPropertiesDto> SelectedDocumentHistories { get; set; } = new();
    private bool AllDocumentHistoriesSelected { get; set; }

    public DocumentHistories()
    {
        NewDocumentHistory = new DocumentHistoryCreateDto();
        EditingDocumentHistory = new DocumentHistoryUpdateDto();
        Filter = new GetDocumentHistoriesInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = "DocumentHistory.CreationTime DESC"
        };
        DocumentHistoryList = new List<DocumentHistoryWithNavigationPropertiesDto>();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["DocumentHistories"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewDocumentHistory"], async () => {
            await OpenCreateDocumentHistoryModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.DocumentHistories.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(DocumentHistoryWithNavigationPropertiesDto documentHistory)
    {
        DataGridRef.ToggleDetailRow(documentHistory, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<DocumentHistoryWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteDocumentHistory;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<DocumentHistoryWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateDocumentHistory = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentHistories.Create);
        CanEditDocumentHistory = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentHistories.Edit);
        CanDeleteDocumentHistory = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentHistories.Delete);
    }

    private async Task GetDocumentHistoriesAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await DocumentHistoriesAppService.GetListAsync(Filter);
        DocumentHistoryList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetDocumentHistoriesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await DocumentHistoriesAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/document-histories/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&Comment={HttpUtility.UrlEncode(Filter.Comment)}&Action={HttpUtility.UrlEncode(Filter.Action)}&DocumentId={Filter.DocumentId}&FromUser={Filter.FromUser}&ToUser={Filter.ToUser}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentHistoryWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentHistoriesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateDocumentHistoryModalAsync()
    {
        NewDocumentHistory = new DocumentHistoryCreateDto
        {
        };
        SelectedCreateTab = "documentHistory-create-tab";
        await NewDocumentHistoryValidations.ClearAll();
        await CreateDocumentHistoryModal.Show();
    }

    private async Task CloseCreateDocumentHistoryModalAsync()
    {
        NewDocumentHistory = new DocumentHistoryCreateDto
        {
        };
        await CreateDocumentHistoryModal.Hide();
    }

    private async Task OpenEditDocumentHistoryModalAsync(DocumentHistoryWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "documentHistory-edit-tab";
        var documentHistory = await DocumentHistoriesAppService.GetWithNavigationPropertiesAsync(input.DocumentHistory.Id);
        EditingDocumentHistoryId = documentHistory.DocumentHistory.Id;
        EditingDocumentHistory = ObjectMapper.Map<DocumentHistoryDto, DocumentHistoryUpdateDto>(documentHistory.DocumentHistory);
        await EditingDocumentHistoryValidations.ClearAll();
        await EditDocumentHistoryModal.Show();
    }

    private async Task DeleteDocumentHistoryAsync(DocumentHistoryWithNavigationPropertiesDto input)
    {
        try
        {
            await DocumentHistoriesAppService.DeleteAsync(input.DocumentHistory.Id);
            await GetDocumentHistoriesAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteDocumentHistoryWithConfirmationAsync(DocumentHistoryWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteDocumentHistoryAsync(input);
        }
    }

    private async Task CreateDocumentHistoryAsync()
    {
        try
        {
            if (await NewDocumentHistoryValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentHistoriesAppService.CreateAsync(NewDocumentHistory);
            await GetDocumentHistoriesAsync();
            await CloseCreateDocumentHistoryModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditDocumentHistoryModalAsync()
    {
        await EditDocumentHistoryModal.Hide();
    }

    private async Task UpdateDocumentHistoryAsync()
    {
        try
        {
            if (await EditingDocumentHistoryValidations.ValidateAll() == false)
            {
                return;
            }

            await DocumentHistoriesAppService.UpdateAsync(EditingDocumentHistoryId, EditingDocumentHistory);
            await GetDocumentHistoriesAsync();
            await EditDocumentHistoryModal.Hide();
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

    protected virtual async Task OnCommentChangedAsync(string? comment)
    {
        Filter.Comment = comment;
        await SearchAsync();
    }

    protected virtual async Task OnActionChangedAsync(string? action)
    {
        Filter.Action = action;
        await SearchAsync();
    }

    protected virtual async Task OnDocumentIdChangedAsync(Guid? documentId)
    {
        Filter.DocumentId = documentId;
        await SearchAsync();
    }

    protected virtual async Task OnFromUserChangedAsync(Guid? fromUser)
    {
        Filter.FromUser = fromUser;
        await SearchAsync();
    }

    protected virtual async Task OnToUserChangedAsync(Guid? toUser)
    {
        Filter.ToUser = toUser;
        await SearchAsync();
    }

    private async Task GetDocumentCollectionLookupAsync(string? newValue = null)
    {
        DocumentsCollection = (await DocumentHistoriesAppService.GetDocumentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await DocumentHistoriesAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllDocumentHistoriesSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllDocumentHistoriesSelected = false;
        SelectedDocumentHistories.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedDocumentHistoryRowsChanged()
    {
        if (SelectedDocumentHistories.Count != PageSize)
        {
            AllDocumentHistoriesSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedDocumentHistoriesAsync()
    {
        var message = AllDocumentHistoriesSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedDocumentHistories.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllDocumentHistoriesSelected)
        {
            await DocumentHistoriesAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await DocumentHistoriesAppService.DeleteByIdsAsync(SelectedDocumentHistories.Select(x => x.DocumentHistory.Id).ToList());
        }

        SelectedDocumentHistories.Clear();
        AllDocumentHistoriesSelected = false;
        await GetDocumentHistoriesAsync();
    }
}