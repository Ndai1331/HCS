using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using HC.Blazor.Components.DepartmentTreeSelect;
using HC.Blazor.Pages;
using HC.Documents;
using HC.Identity;
using HC.Positions;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.AspNetCore.Components.Web.Theming.PageToolbars;
using Volo.Abp.BlazoriseUI;
using Volo.Abp.Data;
using Volo.Abp.Http;
using Volo.Abp.Http.Client;
using Volo.Abp.Identity;

namespace HC.Blazor.Pages.Identity;

public partial class Users
{
    private const string PositionIdPropertyName = "PositionId";

    [Inject] protected IUsersAppService UsersAppService { get; set; } = default!;
    [Inject] protected IIdentityUserAppService IdentityUserAppService { get; set; } = default!;
    [Inject] protected IDocumentsAppService DocumentsAppService { get; set; } = default!;
    [Inject] protected IPositionsAppService PositionsAppService { get; set; } = default!;

    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems { get; } = new();
    protected PageToolbar Toolbar { get; } = new();

    protected bool ShowAdvancedFilters { get; set; }
    protected string FilterText { get; set; } = string.Empty;
    protected Guid? FilterRoleId { get; set; }
    protected Guid? FilterOrganizationUnitId { get; set; }
    protected string? FilterUserName { get; set; }
    protected string? FilterPhoneNumber { get; set; }
    protected string? FilterFullName { get; set; }
    protected bool? FilterIsActive { get; set; }

    protected IReadOnlyList<IdentityUserWithNavigationPropertiesDto> UserList { get; set; } =
        Array.Empty<IdentityUserWithNavigationPropertiesDto>();

    protected int TotalCount { get; set; }
    protected int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    protected int CurrentPage { get; set; } = 1;

    protected List<IdentityRoleDto> AllRoles { get; set; } = new();
    protected List<LookupDto<Guid>> FilterRoleLookups { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedFilterRole { get; set; } = new();
    protected List<LookupDto<Guid>> OrganizationUnitLookups { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedFilterOrganizationUnit { get; set; } = new();

    protected List<string> AllRoleNames { get; set; } = new();
    protected Dictionary<string, bool> RoleSelection { get; set; } = new();
    protected HashSet<string> SelectedRoleNames { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    protected List<DepartmentTreeView> OrganizationUnitTreeViews { get; set; } = new();
    protected List<DepartmentTreeView> AllOrganizationUnitsFlat { get; set; } = new();
    protected List<DepartmentTreeView> SelectedOrganizationUnits { get; set; } = new();

    protected List<LookupDto<Guid>> PositionLookups { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedCreatePosition { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedEditPosition { get; set; } = new();

    protected Modal? CreateModalRef { get; set; }
    protected Modal? EditModalRef { get; set; }
    protected Modal? DetailModalRef { get; set; }
    protected Validations? CreateValidations { get; set; }
    protected Validations? EditValidations { get; set; }

    protected string SelectedCreateTab { get; set; } = "user-info";
    protected string SelectedEditTab { get; set; } = "user-info";
    protected string SelectedDetailTab { get; set; } = "user-info";

    protected IdentityUserCreateDto CreateModel { get; set; } = new();
    protected IdentityUserUpdateDto EditModel { get; set; } = new();
    protected IdentityUserDto? EditingUser { get; set; }
    protected IdentityUserDto? DetailUser { get; set; }
    protected string DetailRolesName { get; set; } = string.Empty;
    protected string? DetailPositionName { get; set; }
    protected List<string> DetailRoleNames { get; set; } = new();
    protected List<DepartmentTreeView> DetailOrganizationUnits { get; set; } = new();

    protected bool CreateShowPassword { get; set; }

    protected Modal? ResetPasswordModalRef { get; set; }
    protected IdentityUserDto? ResetPasswordUser { get; set; }
    protected string NewPasswordValue { get; set; } = string.Empty;
    protected bool ShowNewPassword { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await SetToolbarItemsAsync();
            await LoadLookupDataAsync();
            await SearchAsync();
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["Users"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["NewUser"], OpenCreateModalAsync, IconName.Add,
            requiredPolicyName: IdentityPermissions.Users.Create);
        return ValueTask.CompletedTask;
    }

    private async Task LoadLookupDataAsync()
    {
        var rolesTask = IdentityUserAppService.GetAssignableRolesAsync();
        var ouTreeTask = LoadOrganizationUnitTreeAsync();
        await Task.WhenAll(rolesTask, ouTreeTask);

        AllRoles = rolesTask.Result.Items.ToList();
        FilterRoleLookups = AllRoles
            .Select(r => new LookupDto<Guid> { Id = r.Id, DisplayName = r.Name })
            .ToList();
        OrganizationUnitLookups = AllOrganizationUnitsFlat
            .Select(ou => new LookupDto<Guid> { Id = ou.Id, DisplayName = ou.Name })
            .ToList();
    }

    private async Task LoadOrganizationUnitTreeAsync()
    {
        var organizationUnits = await DocumentsAppService.GetOrganizationUnitTreeAsync();
        var departments = organizationUnits
            .Select(ou => new DepartmentTreeView
            {
                Id = ou.Id,
                ParentId = ou.ParentId?.ToString(),
                Code = ou.Code,
                Name = ou.DisplayName
            })
            .ToList();

        var departmentsDictionary = new Dictionary<string, List<DepartmentTreeView>>();
        foreach (var department in departments)
        {
            var parentId = department.ParentId ?? string.Empty;
            if (!departmentsDictionary.ContainsKey(parentId))
            {
                departmentsDictionary[parentId] = new List<DepartmentTreeView>();
            }

            departmentsDictionary[parentId].Add(department);
        }

        foreach (var department in departments)
        {
            var departmentId = department.Id.ToString();
            department.Children = departmentsDictionary.TryGetValue(departmentId, out var children)
                ? children
                : new List<DepartmentTreeView>();
        }

        OrganizationUnitTreeViews = departmentsDictionary.TryGetValue(string.Empty, out var roots)
            ? roots
            : new List<DepartmentTreeView>();
        DepartmentTreeSelectHelper.ExpandAllNodes(OrganizationUnitTreeViews);
        AllOrganizationUnitsFlat = FlattenDepartments(OrganizationUnitTreeViews);
    }

    private static List<DepartmentTreeView> FlattenDepartments(IEnumerable<DepartmentTreeView> nodes)
    {
        var result = new List<DepartmentTreeView>();
        foreach (var node in nodes)
        {
            result.Add(node);
            if (node.Children?.Any() == true)
            {
                result.AddRange(FlattenDepartments(node.Children));
            }
        }

        return result;
    }

    private async Task OnGridReadAsync(DataGridReadDataEventArgs<IdentityUserWithNavigationPropertiesDto> e)
    {
        CurrentPage = e.Page;
        await SearchAsync();
    }

    protected async Task SearchAsync()
    {
        var input = new GetUsersInput
        {
            FilterText = FilterText,
            RoleId = FilterRoleId,
            OrganizationUnitId = FilterOrganizationUnitId,
            UserName = FilterUserName,
            PhoneNumber = FilterPhoneNumber,
            FullName = FilterFullName,
            IsActive = FilterIsActive,
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize
        };

        var page = await UsersAppService.GetListWithNavigationPropertiesAsync(input);
        UserList = page.Items;
        TotalCount = (int)page.TotalCount;
        await InvokeAsync(StateHasChanged);
    }

    protected async Task ClearFiltersAsync()
    {
        FilterText = string.Empty;
        FilterRoleId = null;
        FilterOrganizationUnitId = null;
        FilterUserName = null;
        FilterPhoneNumber = null;
        FilterFullName = null;
        FilterIsActive = null;
        SelectedFilterRole = new List<LookupDto<Guid>>();
        SelectedFilterOrganizationUnit = new List<LookupDto<Guid>>();
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterRoleChangedAsync(Guid? value)
    {
        FilterRoleId = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterOrganizationUnitChangedAsync(Guid? value)
    {
        FilterOrganizationUnitId = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterUserNameChangedAsync(string? value)
    {
        FilterUserName = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterPhoneNumberChangedAsync(string? value)
    {
        FilterPhoneNumber = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterFullNameChangedAsync(string? value)
    {
        FilterFullName = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    protected async Task OnFilterIsActiveChangedAsync(bool? value)
    {
        FilterIsActive = value;
        CurrentPage = 1;
        await SearchAsync();
    }

    private async Task LoadAllRolesAsync()
    {
        var rolesPage = await IdentityUserAppService.GetAssignableRolesAsync();
        AllRoleNames = rolesPage.Items?
            .Select(r => r.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
        RoleSelection = AllRoleNames.ToDictionary(name => name, _ => false, StringComparer.OrdinalIgnoreCase);
    }

    private void OnRoleCheckedChanged(string roleName, bool isChecked)
    {
        RoleSelection[roleName] = isChecked;
        if (isChecked)
        {
            SelectedRoleNames.Add(roleName);
        }
        else
        {
            SelectedRoleNames.Remove(roleName);
        }
    }

    private async Task<List<LookupDto<Guid>>> FilterPositionLookupAsync(
        IReadOnlyList<LookupDto<Guid>> dbset,
        string filter,
        CancellationToken token)
    {
        var result = await PositionsAppService.GetPositionLookupAsync(new LookupRequestDto
        {
            Filter = filter,
            MaxResultCount = 20,
            SkipCount = 0
        });
        PositionLookups = result.Items.ToList();
        return PositionLookups;
    }

    private async Task<LookupDto<Guid>?> GetPositionByIdAsync(
        IReadOnlyList<LookupDto<Guid>> items,
        string id,
        CancellationToken token)
    {
        if (!Guid.TryParse(id, out var positionId))
        {
            return null;
        }

        var found = items.FirstOrDefault(x => x.Id == positionId);
        if (found != null)
        {
            return found;
        }

        var result = await PositionsAppService.GetPositionLookupAsync(new LookupRequestDto
        {
            MaxResultCount = 1000,
            SkipCount = 0
        });
        return result.Items.FirstOrDefault(x => x.Id == positionId);
    }

    private void OnCreatePositionChanged(List<LookupDto<Guid>> value)
    {
        SelectedCreatePosition = value ?? new List<LookupDto<Guid>>();
    }

    private void OnEditPositionChanged(List<LookupDto<Guid>> value)
    {
        SelectedEditPosition = value ?? new List<LookupDto<Guid>>();
    }

    private static bool TryGetSelectedPositionId(
        IReadOnlyList<LookupDto<Guid>> selected,
        out Guid positionId)
    {
        positionId = selected.FirstOrDefault()?.Id ?? Guid.Empty;
        return positionId != Guid.Empty;
    }

    private string GetUserFriendlyErrorMessage(Exception exception)
    {
        if (exception is AbpRemoteCallException remoteException)
        {
            var validationMessages = remoteException.Error?.ValidationErrors?
                .Select(MapValidationErrorMessage)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .Distinct()
                .ToList();

            if (validationMessages?.Count > 0)
            {
                return string.Join(Environment.NewLine, validationMessages);
            }

            if (!string.IsNullOrWhiteSpace(remoteException.Error?.Message))
            {
                return remoteException.Error.Message;
            }
        }

        if (exception.InnerException != null)
        {
            return GetUserFriendlyErrorMessage(exception.InnerException);
        }

        return exception.Message;
    }

    private string MapValidationErrorMessage(RemoteServiceValidationErrorInfo validationError)
    {
        if (validationError.Members?.Any(member =>
                string.Equals(member, PositionIdPropertyName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(member, "positionId", StringComparison.OrdinalIgnoreCase)) == true)
        {
            return L["PositionRequired"];
        }

        return validationError.Message ?? string.Empty;
    }

    private async Task OpenCreateModalAsync()
    {
        CreateModel = new IdentityUserCreateDto
        {
            IsActive = true,
            LockoutEnabled = false,
            EmailConfirmed = false
        };
        SelectedCreateTab = "user-info";
        SelectedRoleNames.Clear();
        SelectedOrganizationUnits = new List<DepartmentTreeView>();
        SelectedCreatePosition = new List<LookupDto<Guid>>();
        CreateShowPassword = false;
        await LoadAllRolesAsync();
        await LoadOrganizationUnitTreeAsync();
        await CreateModalRef!.Show();
    }

    private Task CloseCreateModalAsync() => CreateModalRef!.Hide();

    private async Task CreateAsync()
    {
        if (CreateValidations != null && !await CreateValidations.ValidateAll())
        {
            return;
        }

        if (!TryGetSelectedPositionId(SelectedCreatePosition, out var createPositionId))
        {
            SelectedCreateTab = "user-info";
            await UiMessageService.Warn(L["PositionRequired"]);
            return;
        }

        try
        {
            var assignableRoles = (await IdentityUserAppService.GetAssignableRolesAsync()).Items
                .Select(r => r.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            CreateModel.RoleNames = SelectedRoleNames
                .Where(name => assignableRoles.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            CreateModel.OrganizationUnitIds = SelectedOrganizationUnits
                .Select(ou => ou.Id)
                .Distinct()
                .ToArray();

            CreateModel.SetProperty(PositionIdPropertyName, createPositionId);

            await IdentityUserAppService.CreateAsync(CreateModel);
            await CloseCreateModalAsync();
            await SearchAsync();
            await UiMessageService.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(GetUserFriendlyErrorMessage(ex));
        }
    }

    private async Task OpenEditModalAsync(IdentityUserDto user)
    {
        EditingUser = user;
        EditModel = ObjectMapper.Map<IdentityUserDto, IdentityUserUpdateDto>(user);
        SelectedEditTab = "user-info";

        await LoadAllRolesAsync();
        await LoadOrganizationUnitTreeAsync();

        var userRoles = await IdentityUserAppService.GetRolesAsync(user.Id);
        SelectedRoleNames = userRoles.Items.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        RoleSelection = AllRoleNames.ToDictionary(
            name => name,
            name => SelectedRoleNames.Contains(name),
            StringComparer.OrdinalIgnoreCase);

        var userOrganizationUnits = await IdentityUserAppService.GetOrganizationUnitsAsync(user.Id);
        var selectedOuIds = userOrganizationUnits.Select(ou => ou.Id).ToHashSet();
        SelectedOrganizationUnits = AllOrganizationUnitsFlat
            .Where(ou => selectedOuIds.Contains(ou.Id))
            .ToList();

        var positionId = user.GetProperty<Guid?>(PositionIdPropertyName);
        SelectedEditPosition = new List<LookupDto<Guid>>();
        if (positionId.HasValue && positionId.Value != Guid.Empty)
        {
            var positionLookup = await PositionsAppService.GetPositionLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 1000,
                SkipCount = 0
            });
            var position = positionLookup.Items.FirstOrDefault(p => p.Id == positionId.Value);
            if (position != null)
            {
                SelectedEditPosition = new List<LookupDto<Guid>> { position };
            }
        }

        await EditModalRef!.Show();
    }

    private Task CloseEditModalAsync() => EditModalRef!.Hide();

    private async Task UpdateAsync()
    {
        if (EditingUser == null)
        {
            return;
        }

        if (EditValidations != null && !await EditValidations.ValidateAll())
        {
            return;
        }

        if (!TryGetSelectedPositionId(SelectedEditPosition, out var editPositionId))
        {
            SelectedEditTab = "user-info";
            await UiMessageService.Warn(L["PositionRequired"]);
            return;
        }

        try
        {
            var assignableRoles = (await IdentityUserAppService.GetAssignableRolesAsync()).Items
                .Select(r => r.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            EditModel.RoleNames = SelectedRoleNames
                .Where(name => assignableRoles.Contains(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            EditModel.OrganizationUnitIds = SelectedOrganizationUnits
                .Select(ou => ou.Id)
                .Distinct()
                .ToArray();

            EditModel.SetProperty(PositionIdPropertyName, editPositionId);

            await IdentityUserAppService.UpdateAsync(EditingUser.Id, EditModel);
            await CloseEditModalAsync();
            await SearchAsync();
            await UiMessageService.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(GetUserFriendlyErrorMessage(ex));
        }
    }

    private async Task OpenDetailModalAsync(IdentityUserWithNavigationPropertiesDto item)
    {
        DetailUser = item.User;
        DetailRolesName = item.RolesName;
        DetailPositionName = item.PositionName;
        SelectedDetailTab = "user-info";

        await LoadOrganizationUnitTreeAsync();
        var userOrganizationUnits = await IdentityUserAppService.GetOrganizationUnitsAsync(item.User.Id);
        var selectedOuIds = userOrganizationUnits.Select(ou => ou.Id).ToHashSet();
        DetailOrganizationUnits = AllOrganizationUnitsFlat
            .Where(ou => selectedOuIds.Contains(ou.Id))
            .ToList();

        var userRoles = await IdentityUserAppService.GetRolesAsync(item.User.Id);
        DetailRoleNames = userRoles.Items.Select(r => r.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        DetailRolesName = FormatRolesName(DetailRoleNames);

        await DetailModalRef!.Show();
    }

    private Task CloseDetailModalAsync() => DetailModalRef!.Hide();

    private async Task DeleteUserAsync(IdentityUserDto user)
    {
        if (await UiMessageService.Confirm(L["UserDeletionConfirmationMessage", user.UserName]))
        {
            await IdentityUserAppService.DeleteAsync(user.Id);
            await SearchAsync();
            await UiMessageService.Success(L["SuccessfullyDeleted"]);
        }
    }

    private async Task OpenResetPasswordModalAsync(IdentityUserDto user)
    {
        ResetPasswordUser = user;
        NewPasswordValue = string.Empty;
        ShowNewPassword = false;
        await ResetPasswordModalRef!.Show();
    }

    private Task CloseResetPasswordModalAsync() => ResetPasswordModalRef!.Hide();

    private void GenerateRandomPassword()
    {
        // Generate a strong random password that satisfies the default identity password policy.
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*";
        var all = upper + lower + digits + special;
        var random = new Random();
        var chars = new List<char>
        {
            upper[random.Next(upper.Length)],
            lower[random.Next(lower.Length)],
            digits[random.Next(digits.Length)],
            special[random.Next(special.Length)]
        };
        for (var i = chars.Count; i < 12; i++)
        {
            chars.Add(all[random.Next(all.Length)]);
        }

        NewPasswordValue = new string(chars.OrderBy(_ => random.Next()).ToArray());
        ShowNewPassword = true;
    }

    private async Task ResetPasswordAsync()
    {
        if (ResetPasswordUser == null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPasswordValue))
        {
            await UiMessageService.Warn(L["NewPasswordRequired"]);
            return;
        }

        try
        {
            await IdentityUserAppService.UpdatePasswordAsync(
                ResetPasswordUser.Id,
                new IdentityUserUpdatePasswordInput { NewPassword = NewPasswordValue });
            await CloseResetPasswordModalAsync();
            await UiMessageService.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message);
        }
    }

    protected void OnSelectedCreateTabChanged(string name) => SelectedCreateTab = name;
    protected void OnSelectedEditTabChanged(string name) => SelectedEditTab = name;
    protected void OnSelectedDetailTabChanged(string name) => SelectedDetailTab = name;

    protected int GetSelectedRoleCount() => SelectedRoleNames.Count;
    protected int GetSelectedOrganizationUnitCount() => SelectedOrganizationUnits.Count;
    protected int GetDetailOrganizationUnitCount() => DetailOrganizationUnits.Count;

    protected static string FormatDateTime(DateTime? dateTime)
    {
        return dateTime?.ToString("dd/MM/yyyy HH:mm:ss") ?? string.Empty;
    }

    protected static string FormatFullName(string? surname, string? name)
    {
        return ((surname ?? string.Empty) + " " + (name ?? string.Empty)).Trim();
    }

    protected static string FormatRolesName(IEnumerable<string> roleNames)
    {
        return string.Join(", ", roleNames.Where(name => !string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
