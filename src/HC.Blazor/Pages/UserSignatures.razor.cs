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
using HC.UserSignatures;
using HC.Permissions;
using HC.Shared;
using HC.SignatureSettings;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.AspNetCore.Components.Messages;
namespace HC.Blazor.Pages;

public partial class UserSignatures
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<UserSignatureWithNavigationPropertiesDto>? DataGridRef { get; set; }

    private IReadOnlyList<UserSignatureWithNavigationPropertiesDto> UserSignatureList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateUserSignature { get; set; }

    private bool CanEditUserSignature { get; set; }

    private bool CanDeleteUserSignature { get; set; }

    private bool IsAdmin { get; set; }

    private UserSignatureCreateDto NewUserSignature { get; set; }

    private UserSignatureUpdateDto EditingUserSignature { get; set; }

    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Validation error keys
    private string? CreateUserSignatureValidationErrorKey { get; set; }
    private string? EditUserSignatureValidationErrorKey { get; set; }

    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);
    private Guid EditingUserSignatureId { get; set; }

    private Modal CreateUserSignatureModal { get; set; } = new();
    private Modal EditUserSignatureModal { get; set; } = new();
    private GetUserSignaturesInput Filter { get; set; }

    private DataGridEntityActionsColumn<UserSignatureWithNavigationPropertiesDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "userSignature-create-tab";
    protected string SelectedEditTab = "userSignature-edit-tab";

    private IReadOnlyList<LookupDto<Guid>> IdentityUsersCollection { get; set; } = new List<LookupDto<Guid>>();
    private IReadOnlyList<LookupDto<Guid>> SignatureSettingsCollection { get; set; } = new List<LookupDto<Guid>>();
    private Dictionary<Guid, string> SignatureSettingsIdToCodeMap { get; set; } = new Dictionary<Guid, string>();
    private Guid? NewSignatureSettingId { get; set; }
    private Guid? EditingProviderSignatureSettingId { get; set; }
    private List<UserSignatureWithNavigationPropertiesDto> SelectedUserSignatures { get; set; } = new();
    private bool AllUserSignaturesSelected { get; set; }

    public UserSignatures()
    {
        NewUserSignature = new UserSignatureCreateDto();
        EditingUserSignature = new UserSignatureUpdateDto();
        Filter = new GetUserSignaturesInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        UserSignatureList = new List<UserSignatureWithNavigationPropertiesDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        await GetIdentityUserCollectionLookupAsync();
        await GetSignatureSettingsCollectionLookupAsync();
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["UserSignatures"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewUserSignature"], async () => {
            await OpenCreateUserSignatureModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.UserSignatures.Create);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(UserSignatureWithNavigationPropertiesDto userSignature)
    {
        DataGridRef.ToggleDetailRow(userSignature, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<UserSignatureWithNavigationPropertiesDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteUserSignature;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<UserSignatureWithNavigationPropertiesDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateUserSignature = await AuthorizationService.IsGrantedAsync(HCPermissions.UserSignatures.Create);
        CanEditUserSignature = await AuthorizationService.IsGrantedAsync(HCPermissions.UserSignatures.Edit);
        CanDeleteUserSignature = await AuthorizationService.IsGrantedAsync(HCPermissions.UserSignatures.Delete);
        // Check if user has default permission - can view all user signatures (admin)
        // If not, user can only view their own signatures
        IsAdmin = await AuthorizationService.IsGrantedAsync(HCPermissions.UserSignatures.Default);
    }

    private async Task GetUserSignaturesAsync()
    {
        // If user is not admin, filter by current user
        if (!IsAdmin && CurrentUser.Id.HasValue)
        {
            Filter.IdentityUserId = CurrentUser.Id.Value;
        }

        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await UserSignaturesAppService.GetListAsync(Filter);
        UserSignatureList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetUserSignaturesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await UserSignaturesAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/user-signatures/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&SignType={HttpUtility.UrlEncode(Filter.SignType?.ToString())}&ProviderCode={HttpUtility.UrlEncode(Filter.ProviderCode)}&TokenRef={HttpUtility.UrlEncode(Filter.TokenRef)}&SignatureImage={HttpUtility.UrlEncode(Filter.SignatureImage)}&ValidFromMin={Filter.ValidFromMin?.ToString("O")}&ValidFromMax={Filter.ValidFromMax?.ToString("O")}&ValidToMin={Filter.ValidToMin?.ToString("O")}&ValidToMax={Filter.ValidToMax?.ToString("O")}&IsActive={Filter.IsActive}&IdentityUserId={Filter.IdentityUserId}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<UserSignatureWithNavigationPropertiesDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetUserSignaturesAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateUserSignatureModalAsync()
    {
        NewUserSignature = new UserSignatureCreateDto
        {
            ValidFrom = DateTime.Now,
            ValidTo = DateTime.Now,
            IdentityUserId = IsAdmin 
                ? IdentityUsersCollection.Select(i => i.Id).FirstOrDefault()
                : (CurrentUser.Id ?? Guid.Empty),
        };
        NewSignatureSettingId = null;
        SelectedCreateTab = "userSignature-create-tab";
        CreateUserSignatureValidationErrorKey = null;
        CreateFieldErrors.Clear();
        await CreateUserSignatureModal.Show();
    }

    private async Task CloseCreateUserSignatureModalAsync()
    {
        NewUserSignature = new UserSignatureCreateDto
        {
            ValidFrom = DateTime.Now,
            ValidTo = DateTime.Now,
            IdentityUserId = IdentityUsersCollection.Select(i => i.Id).FirstOrDefault(),
        };
        await CreateUserSignatureModal.Hide();
    }

    private async Task OpenEditUserSignatureModalAsync(UserSignatureWithNavigationPropertiesDto input)
    {
        SelectedEditTab = "userSignature-edit-tab";
        var userSignature = await UserSignaturesAppService.GetWithNavigationPropertiesAsync(input.UserSignature.Id);
        EditingUserSignatureId = userSignature.UserSignature.Id;
        EditingUserSignature = ObjectMapper.Map<UserSignatureDto, UserSignatureUpdateDto>(userSignature.UserSignature);
        
        // Find SignatureSettingId based on ProviderCode
        EditingProviderSignatureSettingId = SignatureSettingsIdToCodeMap
            .FirstOrDefault(x => x.Value == EditingUserSignature.ProviderCode).Key;
        
        EditUserSignatureValidationErrorKey = null;
        EditFieldErrors.Clear();
        await EditUserSignatureModal.Show();
    }

    private async Task DeleteUserSignatureAsync(UserSignatureWithNavigationPropertiesDto input)
    {
        try
        {
            await UserSignaturesAppService.DeleteAsync(input.UserSignature.Id);
            await GetUserSignaturesAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private async Task DeleteUserSignatureWithConfirmationAsync(UserSignatureWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteUserSignatureAsync(input);
        }
    }

    private async Task CreateUserSignatureAsync()
    {
        try
        {
            if (!ValidateCreateUserSignature())
            {
                await UiMessageService.Warn(L[CreateUserSignatureValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Map SignatureSettingId to ProviderCode
            if (NewSignatureSettingId.HasValue && SignatureSettingsIdToCodeMap.ContainsKey(NewSignatureSettingId.Value))
            {
                NewUserSignature.ProviderCode = SignatureSettingsIdToCodeMap[NewSignatureSettingId.Value];
            }

            // If user is not admin, ensure IdentityUserId is set to current user
            if (!IsAdmin && CurrentUser.Id.HasValue)
            {
                NewUserSignature.IdentityUserId = CurrentUser.Id.Value;
            }

            await UserSignaturesAppService.CreateAsync(NewUserSignature);
            await GetUserSignaturesAsync();
            await CloseCreateUserSignatureModalAsync();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool ValidateCreateUserSignature()
    {
        // Reset error state
        CreateUserSignatureValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: SignType
        if (string.IsNullOrWhiteSpace(NewUserSignature?.SignType))
        {
            CreateFieldErrors["SignType"] = L["SignTypeRequired"];
            CreateUserSignatureValidationErrorKey = "SignTypeRequired";
            isValid = false;
        }

        // Required: SignatureSettingId (ProviderCode)
        if (!NewSignatureSettingId.HasValue || NewSignatureSettingId.Value == Guid.Empty)
        {
            CreateFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            if (isValid)
            {
                CreateUserSignatureValidationErrorKey = "ProviderCodeRequired";
            }
            isValid = false;
        }

        // Required: SignatureImage
        if (string.IsNullOrWhiteSpace(NewUserSignature?.SignatureImage))
        {
            CreateFieldErrors["SignatureImage"] = L["SignatureImageRequired"];
            if (isValid)
            {
                CreateUserSignatureValidationErrorKey = "SignatureImageRequired";
            }
            isValid = false;
        }

        return isValid;
    }

    private async Task CloseEditUserSignatureModalAsync()
    {
        await EditUserSignatureModal.Hide();
    }

    private async Task UpdateUserSignatureAsync()
    {
        try
        {
            if (!ValidateEditUserSignature())
            {
                await UiMessageService.Warn(L[EditUserSignatureValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Map SignatureSettingId to ProviderCode
            if (EditingProviderSignatureSettingId.HasValue && SignatureSettingsIdToCodeMap.ContainsKey(EditingProviderSignatureSettingId.Value))
            {
                EditingUserSignature.ProviderCode = SignatureSettingsIdToCodeMap[EditingProviderSignatureSettingId.Value];
            }

            await UserSignaturesAppService.UpdateAsync(EditingUserSignatureId, EditingUserSignature);
            await GetUserSignaturesAsync();
            await EditUserSignatureModal.Hide();
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    private bool ValidateEditUserSignature()
    {
        // Reset error state
        EditUserSignatureValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        // Required: SignType
        if (string.IsNullOrWhiteSpace(EditingUserSignature?.SignType))
        {
            EditFieldErrors["SignType"] = L["SignTypeRequired"];
            EditUserSignatureValidationErrorKey = "SignTypeRequired";
            isValid = false;
        }

        // Required: SignatureSettingId (ProviderCode)
        if (!EditingProviderSignatureSettingId.HasValue || EditingProviderSignatureSettingId.Value == Guid.Empty)
        {
            EditFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            if (isValid)
            {
                EditUserSignatureValidationErrorKey = "ProviderCodeRequired";
            }
            isValid = false;
        }

        // Required: SignatureImage
        if (string.IsNullOrWhiteSpace(EditingUserSignature?.SignatureImage))
        {
            EditFieldErrors["SignatureImage"] = L["SignatureImageRequired"];
            if (isValid)
            {
                EditUserSignatureValidationErrorKey = "SignatureImageRequired";
            }
            isValid = false;
        }

        return isValid;
    }

    private void OnSelectedCreateTabChanged(string name)
    {
        SelectedCreateTab = name;
    }

    private void OnSelectedEditTabChanged(string name)
    {
        SelectedEditTab = name;
    }

    protected virtual async Task OnSignTypeChangedAsync(string? signType)
    {
        Filter.SignType = signType;
        await SearchAsync();
    }

    // Helper properties for enum conversion
    private SignType? FilterSignType
    {
        get => Enum.TryParse<SignType>(Filter.SignType, out var result) ? result : null;
    }

    private async Task OnFilterSignTypeChangedAsync(SignType? value)
    {
        Filter.SignType = value?.ToString();
        await OnSignTypeChangedAsync(Filter.SignType);
    }

    private SignType? NewSignType
    {
        get => Enum.TryParse<SignType>(NewUserSignature.SignType, out var result) ? result : null;
        set => NewUserSignature.SignType = value?.ToString() ?? string.Empty;
    }

    private SignType? EditingSignType
    {
        get => Enum.TryParse<SignType>(EditingUserSignature.SignType, out var result) ? result : null;
        set => EditingUserSignature.SignType = value?.ToString() ?? string.Empty;
    }

    protected virtual async Task OnProviderCodeChangedAsync(string? providerCode)
    {
        Filter.ProviderCode = providerCode;
        await SearchAsync();
    }

    protected virtual async Task OnTokenRefChangedAsync(string? tokenRef)
    {
        Filter.TokenRef = tokenRef;
        await SearchAsync();
    }

    protected virtual async Task OnSignatureImageChangedAsync(string? signatureImage)
    {
        Filter.SignatureImage = signatureImage;
        await SearchAsync();
    }

    protected virtual async Task OnValidFromMinChangedAsync(DateTime? validFromMin)
    {
        Filter.ValidFromMin = validFromMin.HasValue ? validFromMin.Value.Date : validFromMin;
        await SearchAsync();
    }

    protected virtual async Task OnValidFromMaxChangedAsync(DateTime? validFromMax)
    {
        Filter.ValidFromMax = validFromMax.HasValue ? validFromMax.Value.Date.AddDays(1).AddSeconds(-1) : validFromMax;
        await SearchAsync();
    }

    protected virtual async Task OnValidToMinChangedAsync(DateTime? validToMin)
    {
        Filter.ValidToMin = validToMin.HasValue ? validToMin.Value.Date : validToMin;
        await SearchAsync();
    }

    protected virtual async Task OnValidToMaxChangedAsync(DateTime? validToMax)
    {
        Filter.ValidToMax = validToMax.HasValue ? validToMax.Value.Date.AddDays(1).AddSeconds(-1) : validToMax;
        await SearchAsync();
    }

    protected virtual async Task OnIsActiveChangedAsync(bool? isActive)
    {
        Filter.IsActive = isActive;
        await SearchAsync();
    }

    protected virtual async Task OnIdentityUserIdChangedAsync(Guid? identityUserId)
    {
        Filter.IdentityUserId = identityUserId;
        await SearchAsync();
    }

    private async Task GetIdentityUserCollectionLookupAsync(string? newValue = null)
    {
        IdentityUsersCollection = (await UserSignaturesAppService.GetIdentityUserLookupAsync(new LookupRequestDto { Filter = newValue })).Items;
    }

    private Task SelectAllItems()
    {
        AllUserSignaturesSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllUserSignaturesSelected = false;
        SelectedUserSignatures.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedUserSignatureRowsChanged()
    {
        if (SelectedUserSignatures.Count != PageSize)
        {
            AllUserSignaturesSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedUserSignaturesAsync()
    {
        var message = AllUserSignaturesSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedUserSignatures.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        if (AllUserSignaturesSelected)
        {
            await UserSignaturesAppService.DeleteAllAsync(Filter);
        }
        else
        {
            await UserSignaturesAppService.DeleteByIdsAsync(SelectedUserSignatures.Select(x => x.UserSignature.Id).ToList());
        }

        SelectedUserSignatures.Clear();
        AllUserSignaturesSelected = false;
        await GetUserSignaturesAsync();
    }

    private async Task GetSignatureSettingsCollectionLookupAsync(string? newValue = null)
    {
        var result = await SignatureSettingsAppService.GetSignatureSettingLookupAsync(new LookupRequestDto { Filter = newValue });
        SignatureSettingsCollection = result.Items;
        
        // Build mapper from SignatureSetting Id to ProviderCode
        SignatureSettingsIdToCodeMap.Clear();
        foreach (var item in SignatureSettingsCollection)
        {
            SignatureSettingsIdToCodeMap[item.Id] = item.DisplayName;
        }
    }
}