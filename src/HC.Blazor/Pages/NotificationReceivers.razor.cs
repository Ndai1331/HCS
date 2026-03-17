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
using HC.NotificationReceivers;
using HC.Notifications;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Microsoft.AspNetCore.SignalR;
using HC.Blazor.Hubs;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;

public partial class NotificationReceivers
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<NotificationReceiverWithNavigationPropertiesDto>? DataGridRef { get; set; }

    private IReadOnlyList<NotificationReceiverWithNavigationPropertiesDto> NotificationReceiverList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanEditNotificationReceiver { get; set; }

    private bool CanDeleteNotificationReceiver { get; set; }

    private bool IsMarkingAllAsRead { get; set; }

    [Inject] private IHubContext<NotificationHub> HubContext { get; set; } = null!;

    private NotificationReceiverUpdateDto EditingNotificationReceiver { get; set; }

    private Validations EditingNotificationReceiverValidations { get; set; } = new();
    private Guid EditingNotificationReceiverId { get; set; }

    private Modal EditNotificationReceiverModal { get; set; } = new();
    private GetNotificationReceiversInput Filter { get; set; }

    private DataGridEntityActionsColumn<NotificationReceiverWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    private List<NotificationReceiverWithNavigationPropertiesDto> SelectedNotificationReceivers { get; set; } = new();
    private bool AllNotificationReceiversSelected { get; set; }

    public NotificationReceivers()
    {
        EditingNotificationReceiver = new NotificationReceiverUpdateDto();
        Filter = new GetNotificationReceiversInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        NotificationReceiverList = new List<NotificationReceiverWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        // Ensure only current user's notifications are loaded
        Filter.IdentityUserId = CurrentUser.Id;
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["NotificationReceivers"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["MarkAllAsRead"], async () => {
            await MarkAllAsReadAsync();
        }, IconName.Check);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(NotificationReceiverWithNavigationPropertiesDto notificationReceiver)
    {
        if (DataGridRef != null)
        {
            DataGridRef.ToggleDetailRow(notificationReceiver, true);
        }
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<NotificationReceiverWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteNotificationReceiver;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<NotificationReceiverWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanEditNotificationReceiver = await AuthorizationService.IsGrantedAsync(HCPermissions.NotificationReceivers.Edit);
        CanDeleteNotificationReceiver = await AuthorizationService.IsGrantedAsync(HCPermissions.NotificationReceivers.Delete);
    }

    private async Task GetNotificationReceiversAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        // Always filter by current user - security requirement
        Filter.IdentityUserId = CurrentUser.Id;
        var result = await NotificationReceiversAppService.GetListAsync(Filter);
        NotificationReceiverList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetNotificationReceiversAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await NotificationReceiversAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/notification-receivers/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&IsRead={Filter.IsRead}&ReadAtMin={Filter.ReadAtMin?.ToString("O")}&ReadAtMax={Filter.ReadAtMax?.ToString("O")}&NotificationId={Filter.NotificationId}&IdentityUserId={Filter.IdentityUserId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<NotificationReceiverWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetNotificationReceiversAsync();
        await InvokeAsync(StateHasChanged);
    }


    private async Task OpenEditNotificationReceiverModalAsync(NotificationReceiverWithNavigationPropertiesDto input)
    {
        var notificationReceiver = await NotificationReceiversAppService.GetWithNavigationPropertiesAsync(input.NotificationReceiver.Id);
        EditingNotificationReceiverId = notificationReceiver.NotificationReceiver.Id;
        EditingNotificationReceiver = ObjectMapper.Map<NotificationReceiverDto, NotificationReceiverUpdateDto>(notificationReceiver.NotificationReceiver);
        await EditingNotificationReceiverValidations.ClearAll();
        await EditNotificationReceiverModal.Show();
    }

    private async Task DeleteNotificationReceiverAsync(NotificationReceiverWithNavigationPropertiesDto input)
    {
        try
        {
            await NotificationReceiversAppService.DeleteAsync(input.NotificationReceiver.Id);
            await GetNotificationReceiversAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteNotificationReceiverWithConfirmationAsync(NotificationReceiverWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteNotificationReceiverAsync(input);
        }
    }


    private async Task CloseEditNotificationReceiverModalAsync()
    {
        await EditNotificationReceiverModal.Hide();
    }

    private async Task UpdateNotificationReceiverAsync()
    {
        try
        {
            if (await EditingNotificationReceiverValidations.ValidateAll() == false)
            {
                return;
            }

            await NotificationReceiversAppService.UpdateAsync(EditingNotificationReceiverId, EditingNotificationReceiver);
            await GetNotificationReceiversAsync();
            await EditNotificationReceiverModal.Hide();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }


    private string IsReadFilterValue { get; set; } = string.Empty;

    protected virtual async Task OnIsReadChangedAsync(string? isRead)
    {
        Filter.IsRead = isRead switch
        {
            "True" or "true" => true,
            "False" or "false" => false,
            _ => null
        };
        IsReadFilterValue = isRead ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnReadAtMinChangedAsync(DateTime? readAtMin)
    {
        Filter.ReadAtMin = readAtMin.HasValue ? readAtMin.Value.Date : readAtMin;
        await SearchAsync();
    }

    protected virtual async Task OnReadAtMaxChangedAsync(DateTime? readAtMax)
    {
        Filter.ReadAtMax = readAtMax.HasValue ? readAtMax.Value.Date.AddDays(1).AddSeconds(-1) : readAtMax;
        await SearchAsync();
    }

    protected virtual async Task OnNotificationIdChangedAsync(Guid? notificationId)
    {
        Filter.NotificationId = notificationId;
        await SearchAsync();
    }

    protected virtual async Task OnSourceTypeChangedAsync(string? sourceType)
    {
        Filter.SourceType = sourceType;
        await SearchAsync();
    }

    private Task SelectAllItems()
    {
        AllNotificationReceiversSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllNotificationReceiversSelected = false;
        SelectedNotificationReceivers.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedNotificationReceiverRowsChanged()
    {
        if (SelectedNotificationReceivers.Count != PageSize)
        {
            AllNotificationReceiversSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedNotificationReceiversAsync()
    {
        var message = AllNotificationReceiversSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedNotificationReceivers.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllNotificationReceiversSelected)
        {
            await NotificationReceiversAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await NotificationReceiversAppService.DeleteByIdsAsync(SelectedNotificationReceivers.Select(x => x.NotificationReceiver.Id).ToList());
        }

        SelectedNotificationReceivers.Clear();
        AllNotificationReceiversSelected = false;
        await GetNotificationReceiversAsync();
    }

    private async Task MarkAllAsReadAsync()
    {
        IsMarkingAllAsRead = true;
        try
        {
            await NotificationReceiversAppService.MarkAllAsReadAsync(Filter.SourceType);
            await UiMessageService.Success(L["SuccessfullyMarkedAllAsRead"].Value,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            await GetNotificationReceiversAsync();
            // Send SignalR message to refresh unread count only on success
            if (CurrentUser.Id.HasValue)
            {
                await HubContext.Clients.User(CurrentUser.Id.Value.ToString())
                    .SendAsync("UnreadCountChanged");
            }
        }
        catch
        {
            await UiMessageService.Error(L["ErrorMarkingAllAsRead"].Value,
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
            // Don't refresh on error
        }
        finally
        {
            IsMarkingAllAsRead = false;
        }
    }

    private async Task MarkAsReadAsync(NotificationReceiverWithNavigationPropertiesDto item)
    {
        try
        {
            var updateDto = ObjectMapper.Map<NotificationReceiverDto, NotificationReceiverUpdateDto>(item.NotificationReceiver);
            updateDto.IsRead = true;
            updateDto.ReadAt = DateTime.UtcNow;
            await NotificationReceiversAppService.UpdateAsync(item.NotificationReceiver.Id, updateDto);
            await GetNotificationReceiversAsync();
            // Send SignalR message to refresh unread count only on success
            if (CurrentUser.Id.HasValue)
            {
                await HubContext.Clients.User(CurrentUser.Id.Value.ToString())
                    .SendAsync("UnreadCountChanged");
            }
        }
        catch
        {
            await HandleErrorAsync(new Exception("Failed to mark notification as read"));
            // Don't refresh on error
        }
    }

    private void ViewNotificationDetail(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.RelatedId))
            return;

        var url = GetRelatedUrl(notification);
        if (url != "#")
        {
            NavigationManager.NavigateTo(url);
        }
    }

    private string GetLocalizedTitle(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.Title))
            return string.Empty;
        try
        {
            var localized = L[notification.Title];
            return localized?.Value ?? notification.Title;
        }
        catch
        {
            return notification.Title;
        }
    }

    private string GetLocalizedContent(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.Content))
            return string.Empty;

        var parts = notification.Content.Split('|');
        if (parts.Length > 1)
        {
            var key = parts[0];
            var parameters = parts.Skip(1).ToArray();
            try
            {
                var localizedString = L[key]?.Value;
                if (string.IsNullOrEmpty(localizedString))
                {
                    return notification.Content;
                }
                return string.Format(localizedString, parameters);
            }
            catch
            {
                return notification.Content;
            }
        }
        else
        {
            try
            {
                var localized = L[notification.Content];
                return localized?.Value ?? notification.Content;
            }
            catch
            {
                return notification.Content;
            }
        }
    }

    private string GetRelatedUrl(NotificationDto notification)
    {
        if (string.IsNullOrEmpty(notification.RelatedId) || string.IsNullOrEmpty(notification.RelatedType))
            return "#";

        var url = notification.RelatedType.ToUpper() switch
        {
            "TASK" => $"/project-task-detail/{notification.RelatedId}",
            "PROJECT" => $"/project-detail/{notification.RelatedId}",
            "DOCUMENT" => $"/document-detail/{notification.RelatedId}",
            "CALENDAR_EVENT" => $"/calendar-event-detail/{notification.RelatedId}",
            _ => "#"
        };
        return url ?? "#";
    }
}