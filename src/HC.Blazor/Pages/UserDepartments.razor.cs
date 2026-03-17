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
using HC.UserDepartments;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;

namespace HC.Blazor.Pages;

public partial class UserDepartments
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<UserDepartmentWithNavigationPropertiesDto> DataGridRef { get; set; }

    private IReadOnlyList<UserDepartmentWithNavigationPropertiesDto> UserDepartmentList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateUserDepartment { get; set; }

    private bool CanEditUserDepartment { get; set; }

    private bool CanDeleteUserDepartment { get; set; }

    private UserDepartmentCreateDto NewUserDepartment { get; set; }

    private Validations NewUserDepartmentValidations { get; set; } = new();
    private UserDepartmentUpdateDto EditingUserDepartment { get; set; }

    private Validations EditingUserDepartmentValidations { get; set; } = new();
    private Guid EditingUserDepartmentId { get; set; }

    private Modal CreateUserDepartmentModal { get; set; } = new();
    private Modal EditUserDepartmentModal { get; set; } = new();
    private GetUserDepartmentsInput Filter { get; set; }

    private DataGridEntityActionsColumn<UserDepartmentWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "userDepartment-create-tab";
    protected string SelectedEditTab = "userDepartment-edit-tab";

    private IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private List<UserDepartmentWithNavigationPropertiesDto> SelectedUserDepartments { get; set; } = new();
    private bool AllUserDepartmentsSelected { get; set; }

    public UserDepartments()
    {
        NewUserDepartment = new UserDepartmentCreateDto();
        EditingUserDepartment = new UserDepartmentUpdateDto();
        Filter = new GetUserDepartmentsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        UserDepartmentList = new List<UserDepartmentWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetDepartmentCollectionLookupAsync();
        await GetIdentityUserCollectionLookupAsync();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["UserDepartments"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewUserDepartment"], async () => {
            await OpenCreateUserDepartmentModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.UserDepartments.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(UserDepartmentWithNavigationPropertiesDto userDepartment)
    {
        DataGridRef.ToggleDetailRow(userDepartment, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<UserDepartmentWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteUserDepartment;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<UserDepartmentWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateUserDepartment = await AuthorizationService.IsGrantedAsync(HCPermissions.UserDepartments.Create);
        CanEditUserDepartment = await AuthorizationService.IsGrantedAsync(HCPermissions.UserDepartments.Edit);
        CanDeleteUserDepartment = await AuthorizationService.IsGrantedAsync(HCPermissions.UserDepartments.Delete);
    }

    private async Task GetUserDepartmentsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await UserDepartmentsAppService.GetListAsync(Filter);
        UserDepartmentList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetUserDepartmentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await UserDepartmentsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/user-departments/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&IsPrimary={Filter.IsPrimary}&IsActive={Filter.IsActive}&DepartmentId={Filter.DepartmentId}&UserId={Filter.UserId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<UserDepartmentWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetUserDepartmentsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateUserDepartmentModalAsync()
    {
        NewUserDepartment = new UserDepartmentCreateDto
        {
        };
        SelectedCreateTab = "userDepartment-create-tab";
        await NewUserDepartmentValidations.ClearAll();
        await GetDepartmentCollectionLookupAsync(string.Empty);
        await GetIdentityUserCollectionLookupAsync(string.Empty);
        await CreateUserDepartmentModal.Show();
    }

    private async Task CloseCreateUserDepartmentModalAsync()
    {
        NewUserDepartment = new UserDepartmentCreateDto
        {
        };
        await CreateUserDepartmentModal.Hide();
    }

    private async Task OpenEditUserDepartmentModalAsync(UserDepartmentWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "userDepartment-edit-tab";
        var userDepartment = await UserDepartmentsAppService.GetWithNavigationPropertiesAsync(input.UserDepartment.Id);
        EditingUserDepartmentId = userDepartment.UserDepartment.Id;
        EditingUserDepartment = ObjectMapper.Map<UserDepartmentDto, UserDepartmentUpdateDto>(userDepartment.UserDepartment);
        await GetDepartmentCollectionLookupAsync(userDepartment.Department.Name);
        await GetIdentityUserCollectionLookupAsync(userDepartment.User.Name);
        await EditingUserDepartmentValidations.ClearAll();
        await EditUserDepartmentModal.Show();
    }

    private async Task DeleteUserDepartmentAsync(UserDepartmentWithNavigationPropertiesDto input)
    {
        try
        {
            await UserDepartmentsAppService.DeleteAsync(input.UserDepartment.Id);
            await GetUserDepartmentsAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CreateUserDepartmentAsync()
    {
        try
        {
            if (await NewUserDepartmentValidations.ValidateAll() == false)
            {
                return;
            }

            await UserDepartmentsAppService.CreateAsync(NewUserDepartment);
            await GetUserDepartmentsAsync();
            await CloseCreateUserDepartmentModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task CloseEditUserDepartmentModalAsync()
    {
        await EditUserDepartmentModal.Hide();
    }

    private async Task UpdateUserDepartmentAsync()
    {
        try
        {
            if (await EditingUserDepartmentValidations.ValidateAll() == false)
            {
                return;
            }

            await UserDepartmentsAppService.UpdateAsync(EditingUserDepartmentId, EditingUserDepartment);
            await GetUserDepartmentsAsync();
            await EditUserDepartmentModal.Hide();
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

    protected virtual async Task OnIsPrimaryChangedAsync(bool? isPrimary)
    {
        Filter.IsPrimary = isPrimary;
        await SearchAsync();
    }

    private string IsActiveFilterValue { get; set; } = string.Empty;

    protected virtual async Task OnIsActiveChangedAsync(string? isActive)
    {
        Filter.IsActive = isActive switch
        {
            "True" or "true" => true,
            "False" or "false" => false,
            _ => null
        };
        IsActiveFilterValue = isActive ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnDepartmentIdChangedAsync(Guid? departmentId)
    {
        Filter.DepartmentId = departmentId;
        await SearchAsync();
    }

    protected virtual async Task OnUserIdChangedAsync(Guid? userId)
    {
        Filter.UserId = userId;
        await SearchAsync();
    }

    private async Task GetDepartmentCollectionLookupAsync(string? newValue = null)
    {
        DepartmentsCollection = (await UserDepartmentsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }
 
    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await UserDepartmentsAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllUserDepartmentsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllUserDepartmentsSelected = false;
        SelectedUserDepartments.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedUserDepartmentRowsChanged()
    {
        if (SelectedUserDepartments.Count != PageSize)
        {
            AllUserDepartmentsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedUserDepartmentsAsync()
    {
        var message = AllUserDepartmentsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedUserDepartments.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllUserDepartmentsSelected)
        {
            await UserDepartmentsAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await UserDepartmentsAppService.DeleteByIdsAsync(SelectedUserDepartments.Select(x => x.UserDepartment.Id).ToList());
        }

        SelectedUserDepartments.Clear();
        AllUserDepartmentsSelected = false;
        await GetUserDepartmentsAsync();
    }
}