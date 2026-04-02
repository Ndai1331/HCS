using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Globalization;
using Blazorise;
using Blazorise.DataGrid;
using Volo.Abp.BlazoriseUI.Components;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using HC.DocumentHistories;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Volo.Abp.AspNetCore.Components.Web;

namespace HC.Blazor.Pages;

public partial class MyDocuments
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<DocumentHistoryWithNavigationPropertiesDto> DataGridRef { get; set; } = new();

    private IReadOnlyList<DocumentHistoryWithNavigationPropertiesDto> DocumentHistoryList { get; set; } = new List<DocumentHistoryWithNavigationPropertiesDto>();

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanViewDocumentHistory { get; set; }

    private GetDocumentHistoriesInput Filter { get; set; } = new GetDocumentHistoriesInput();

    private DataGridEntityActionsColumn<DocumentHistoryWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    public MyDocuments()
    {
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        Logger.LogInformation("MyDocuments OnAfterRenderAsync start");

        if (firstRender)
        {
            Logger.LogInformation("MyDocuments OnAfterRenderAsync firstRender");
            await SetPermissionsAsync();
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            // Document histories load only via DataGrid ReadData -> OnDataGridReadAsync (avoid duplicate GET on first paint)
            await InvokeAsync(StateHasChanged);
        }
        Logger.LogInformation("MyDocuments OnAfterRenderAsync end");
    }

    private async Task SetPermissionsAsync()
    {
        CanViewDocumentHistory = await AuthorizationService.IsGrantedAsync(HCPermissions.DocumentHistories.Default);
        await Task.CompletedTask;
    }

    private async Task SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Menu:MyDocuments"]));
        await InvokeAsync(StateHasChanged);
    }

    private async Task SetToolbarItemsAsync()
    {
        Toolbar.AddButton(
                L["Refresh"],
                GetDocumentHistoriesAsync,
                IconName.Sync,
                Color.Primary
        );
        await Task.CompletedTask;
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<DocumentHistoryWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetDocumentHistoriesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OnFilterTextChangedAsync(string value)
    {
        Filter.FilterText = value;
        await Task.CompletedTask;
    }

    private async Task OnActionChangedAsync(string value)
    {
        Filter.Action = value;
        await Task.CompletedTask;
    }

    private async Task OnCommentChangedAsync(string value)
    {
        Filter.Comment = value;
        await Task.CompletedTask;
    }

    private async Task GetDocumentHistoriesAsync()
    {
        try
        {
            Logger.LogInformation("GetDocumentHistoriesAsync start");
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);

            Filter.MaxResultCount = PageSize;
            Filter.SkipCount = (CurrentPage - 1) * PageSize;
            Filter.Sorting = CurrentSorting;

            // Get current user ID
            var currentUserId = CurrentUser.Id ?? Guid.Empty;
            if (currentUserId == Guid.Empty)
            {
                Logger.LogWarning("Current user ID is empty");
                return;
            }

            // Filter by ToUser = CurrentUser
            Filter.ToUser = currentUserId;

            var result = await DocumentHistoriesAppService.GetListAsync(Filter);
            TotalCount = (int)result.TotalCount;
            DocumentHistoryList = result.Items;

            Logger.LogInformation("GetDocumentHistoriesAsync completed: Count={Count}", result.TotalCount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error in GetDocumentHistoriesAsync");
            await HandleErrorAsync(ex);
        }
        finally
        {
            await BlockUiService.UnBlock();
        }
    }

    private async Task ViewDocumentAsync(DocumentHistoryWithNavigationPropertiesDto documentHistory)
    {
        if (documentHistory?.Document != null)
        {
            NavigationManager.NavigateTo($"/document-detail/{documentHistory.Document.Id}");
        }
        await Task.CompletedTask;
    }
}
