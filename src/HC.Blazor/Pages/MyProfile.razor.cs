using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Volo.Abp.AuditLogging;
using Volo.Abp.AuditLogging.Blazor.Pages.Shared.AverageExecutionDurationPerDayWidget;
using Volo.Abp.AuditLogging.Blazor.Pages.Shared.ErrorRateWidget;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.Identity;
using Volo.Saas.Host;
using Volo.Saas.Host.Blazor.Pages.Shared.Components.SaasEditionPercentageWidget;
using Volo.Saas.Host.Blazor.Pages.Shared.Components.SaasLatestTenantsWidget;
using HC.UserSignatures;
using HC.Shared;
using Blazorise;
using Volo.Abp.AspNetCore.Components.Messages;
using BreadcrumbItem = Volo.Abp.BlazoriseUI.BreadcrumbItem;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components.Forms;
using Volo.Abp.Account;
using Volo.Abp.Content;
using System.Threading;
using Microsoft.Extensions.Caching.Memory;
using System.IO;
using Volo.Abp.BlobStoring;
using HC.Blazor.BlobStoring;
using HC.BlobStoring;
using Volo.Abp.Application.Dtos;
using HC.SignatureSettings;


namespace HC.Blazor.Pages;

public partial class MyProfile
{
    [Inject]
    public IPermissionChecker PermissionChecker { get; set; } = default!;

    [Inject]
    public IIdentityUserAppService IdentityUserAppService { get; set; } = default!;
    
    [Inject]
    public Volo.Abp.Account.IAccountAppService ProfileAppService { get; set; } = default!;

    [Inject]
    public IProfileAppService PersonalProfileAppService { get; set; } = default!;

    [Inject]
    public NavigationManager NavigationManager { get; set; } = default!;

    [Inject]
    public IUserSignaturesAppService UserSignaturesAppService { get; set; } = default!;

    [Inject]
    public IUiMessageService UiMessageService { get; set; } = default!;

    [Inject]
    public IJSRuntime JSRuntime { get; set; } = default!;

    [Inject]
    public IMemoryCache MemoryCache { get; set; } = default!;

    [Inject]
    public IBlobContainer BlobContainer { get; set; } = default!;

    [Inject]
    public IBlobDisplayUrlProvider BlobDisplayUrlProvider { get; set; } = default!;

    [Inject]
    public ISignatureSettingsAppService SignatureSettingsAppService { get; set; } = default!;

    protected List<BreadcrumbItem> BreadcrumbItems = new();

    protected IdentityUserUpdateDto ProfileModel { get; set; } = new();

    protected string CurrentPassword { get; set; } = string.Empty;
    protected string NewPassword { get; set; } = string.Empty;
    protected string ConfirmNewPassword { get; set; } = string.Empty;

    protected bool ShowCurrentPassword { get; set; } = false;
    protected bool ShowNewPassword { get; set; } = false;
    protected bool ShowConfirmNewPassword { get; set; } = false;

    protected List<UserSignatureWithNavigationPropertiesDto> UserSignatures { get; set; } = new();
    protected UserSignatureCreateDto NewUserSignature { get; set; } = new();
    protected UserSignatureUpdateDto EditingUserSignature { get; set; } = new();
    protected Guid EditingUserSignatureId { get; set; }
    protected Modal CreateUserSignatureModal { get; set; } = new();
    protected Modal EditUserSignatureModal { get; set; } = new();
    
    // Signature Settings Lookup
    protected IReadOnlyList<LookupDto<Guid>> SignatureSettingsCollection { get; set; } = new List<LookupDto<Guid>>();
    protected Dictionary<Guid, string> SignatureSettingsIdToCodeMap { get; set; } = new Dictionary<Guid, string>();
    protected Dictionary<Guid, string> SignatureSettingsIdToProviderTypeMap { get; set; } = new Dictionary<Guid, string>();
    protected List<LookupDto<Guid>> SelectedSignatureSettingForCreate { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedSignatureSettingForEdit { get; set; } = new();
    
    protected Dictionary<string, string?> CreateSignatureFieldErrors { get; set; } = new();
    protected Dictionary<string, string?> EditSignatureFieldErrors { get; set; } = new();
    protected string? CreateSignatureValidationErrorKey { get; set; }
    protected string? EditSignatureValidationErrorKey { get; set; }

    // File upload for signature image
    protected FilePicker CreateSignatureImageFilePicker { get; set; } = new();
    protected FilePicker EditSignatureImageFilePicker { get; set; } = new();
    protected FilePicker CreateSealImageFilePicker { get; set; } = new();
    protected FilePicker EditSealImageFilePicker { get; set; } = new();
    protected IFileEntry? SelectedSignatureImageFile { get; set; }
    protected string UploadedSignatureImagePath { get; set; } = string.Empty;
    protected bool IsUploadingSignatureImage { get; set; }
    protected int SignatureImageFilePickerProgress { get; set; }

    // Avatar upload
    protected string AvatarUrl { get; set; } = string.Empty;

    /// <summary>Cache-buster for profile picture URL (sidebar / browser cache).</summary>
    protected long ProfilePictureCacheBuster { get; set; }
    
    protected AuditLoggingErrorRateWidgetComponent? ErrorRateWidgetComponent;

    protected AuditLoggingAverageExecutionDurationPerDayWidgetComponent? AverageExecutionDurationPerDayWidgetComponent;

    protected SaasEditionPercentageWidgetComponent? SaasEditionPercentageWidgetComponent;

    protected SaasLatestTenantsWidgetComponent? SaasLatestTenantsWidgetComponent;

    protected DateTime StartDate { get; set; }

    protected DateTime EndDate { get; set; }

    protected bool HasAuditLoggingPermission { get; set; }

    protected bool HasSaasPermission { get; set; }

    protected string SelectedTab { get; set; } = "Profile";
    protected string SelectedTabMyProfile { get; set; } = "Profile";

    protected bool IsLoadingProfileTab { get; set; } = true;
    protected bool IsLoadingUserSignaturesTab { get; set; } = true;

    protected async override Task OnInitializedAsync()
    {
        StartDate = Clock.Now.AddMonths(-1).Date;
        EndDate = Clock.Now.Date;
        HasAuditLoggingPermission = await PermissionChecker.IsGrantedAsync(AbpAuditLoggingPermissions.AuditLogs.Default);
        HasSaasPermission = await PermissionChecker.IsGrantedAsync(SaasHostPermissions.Tenants.Default);

        await Task.WhenAll(
            LoadUserProfileAsync(),
            LoadUserSignaturesAsync());
    }

    protected virtual async Task LoadUserProfileAsync()
    {
        IsLoadingProfileTab = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            if (CurrentUser.Id.HasValue)
            {
                var user = await IdentityUserAppService.GetAsync(CurrentUser.Id.Value);
                if (user != null)
                {
                    ProfileModel = ObjectMapper.Map<IdentityUserDto, IdentityUserUpdateDto>(user);
                    ProfilePictureCacheBuster = DateTime.UtcNow.Ticks;
                }
            }
        }
        finally
        {
            IsLoadingProfileTab = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual string GetSidebarProfileDisplayName()
    {
        var surname = ProfileModel.Surname?.Trim() ?? string.Empty;
        var name = ProfileModel.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(surname) && string.IsNullOrEmpty(name))
        {
            return CurrentUser.Name ?? CurrentUser.UserName ?? string.Empty;
        }
        return string.Join(' ', new[] { surname, name }.Where(static s => s.Length > 0));
    }

    protected virtual async Task SaveProfileAsync()
    {
        
        if (CurrentUser.Id.HasValue)
        {
            try{
                // Account profile API refreshes the signed-in principal so navbar / claims stay in sync.
                await PersonalProfileAppService.UpdateAsync(new UpdateProfileDto
                {
                    UserName = ProfileModel.UserName,
                    Email = ProfileModel.Email,
                    Name = ProfileModel.Name,
                    Surname = ProfileModel.Surname,
                    PhoneNumber = ProfileModel.PhoneNumber,
                    ConcurrencyStamp = ProfileModel.ConcurrencyStamp,
                });
                await LoadUserProfileAsync();
                await Message.Success(L["SuccessfullySaved"]);
                var returnUrl = NavigationManager.ToBaseRelativePath(NavigationManager.Uri);
                if (string.IsNullOrEmpty(returnUrl))
                {
                    returnUrl = "my-profile";
                }
                if (!returnUrl.StartsWith('/'))
                {
                    returnUrl = "/" + returnUrl;
                }
                NavigationManager.NavigateTo(
                    $"/hc/auth/refresh-claims?returnUrl={Uri.EscapeDataString(returnUrl)}",
                    forceLoad: true);
            }
            catch (Exception ex)
            {
                await Message.Error(ex.Message);
            }
        }
    }

    protected virtual async Task ChangePasswordAsync()
    {
        if (NewPassword != ConfirmNewPassword)
        {
            await Message.Error(L["PasswordsDoNotMatch"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentPassword))
        {
            await Message.Error(L["CurrentPasswordRequired"]);
            return;
        }

        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            await Message.Error(L["NewPasswordRequired"]);
            return;
        }

        try
        {
            await PersonalProfileAppService.ChangePasswordAsync(new ChangePasswordInput
            {
                CurrentPassword = CurrentPassword,
                NewPassword = NewPassword
            });

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;

            await Message.Success(L["PasswordChangedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await SetBreadcrumbItemsAsync();
            await InvokeAsync(StateHasChanged);
        }
    } 

    protected virtual async Task RefreshAsync()
    {
        if (HasAuditLoggingPermission)
        {
            if (ErrorRateWidgetComponent != null)
            {
                await ErrorRateWidgetComponent.RefreshAsync();
            }

            if(AverageExecutionDurationPerDayWidgetComponent != null)
            {
                await AverageExecutionDurationPerDayWidgetComponent.RefreshAsync();
            }
        }

        if (HasSaasPermission && SaasEditionPercentageWidgetComponent != null)
        {
            await SaasEditionPercentageWidgetComponent.RefreshAsync();
        }
    }

    protected virtual ValueTask SetBreadcrumbItemsAsync()
    {
        BreadcrumbItems.Add(new BreadcrumbItem(L["Dashboard"]));
        return ValueTask.CompletedTask;
    }

    protected virtual Task OnDatesChangedAsync(IReadOnlyList<DateTime> dates)
    {
        StartDate = dates.Min();
        EndDate = dates.Max();

        return Task.CompletedTask;
    }
    protected virtual async Task OnTabChanged(string tabName)
    {
        SelectedTab = tabName;
        await InvokeAsync(StateHasChanged);
    }   
    protected virtual async Task OnTabChangedMyProfile(string tabName)
    {
        SelectedTabMyProfile = tabName;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task ToggleCurrentPasswordVisibility()
    {
        ShowCurrentPassword = !ShowCurrentPassword;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task ToggleNewPasswordVisibility()
    {
        ShowNewPassword = !ShowNewPassword;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task ToggleConfirmNewPasswordVisibility()
    {
        ShowConfirmNewPassword = !ShowConfirmNewPassword;
        await InvokeAsync(StateHasChanged);
    }

    // UserSignature Methods
    protected virtual async Task LoadUserSignaturesAsync()
    {
        IsLoadingUserSignaturesTab = true;
        await InvokeAsync(StateHasChanged);
        try
        {
            if (CurrentUser.Id.HasValue)
            {
                var result = await UserSignaturesAppService.GetListAsync(new GetUserSignaturesInput
                {
                    IdentityUserId = CurrentUser.Id.Value,
                    MaxResultCount = 100,
                    SkipCount = 0,
                    Sorting = string.Empty
                });
                UserSignatures = result.Items.ToList();
            }
        }
        finally
        {
            IsLoadingUserSignaturesTab = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual async Task OpenCreateUserSignatureModalAsync()
    {
        NewUserSignature = new UserSignatureCreateDto
        {
            ValidFrom = DateTime.Now,
            ValidTo = DateTime.Now.AddYears(1),
            IdentityUserId = CurrentUser.Id ?? Guid.Empty,
            IsActive = true
        };
        CreateSignatureValidationErrorKey = null;
        CreateSignatureFieldErrors.Clear();
        SelectedSignatureSettingForCreate.Clear();
        SignatureSettingsCollection = new List<LookupDto<Guid>>();
        SignatureSettingsIdToCodeMap.Clear();
        await CreateUserSignatureModal.Show();
    }

    protected virtual async Task CloseCreateUserSignatureModalAsync()
    {
        await CreateUserSignatureModal.Hide();
    }

    protected virtual async Task CreateUserSignatureAsync()
    {
        try
        {
            if (!ValidateCreateUserSignature())
            {
                await UiMessageService.Warn(L[CreateSignatureValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Map SignatureSettingId to ProviderCode
            if (SelectedSignatureSettingForCreate != null && SelectedSignatureSettingForCreate.Any())
            {
                var selectedId = SelectedSignatureSettingForCreate.First().Id;
                if (SignatureSettingsIdToCodeMap.ContainsKey(selectedId))
                {
                    NewUserSignature.ProviderCode = SignatureSettingsIdToCodeMap[selectedId];
                }
            }

            if (CurrentUser.Id.HasValue)
            {
                NewUserSignature.IdentityUserId = CurrentUser.Id.Value;
            }

            ClearSealImageIfElectronicSignature(NewUserSignature);

            await UserSignaturesAppService.CreateAsync(NewUserSignature);
            await LoadUserSignaturesAsync();
            await CloseCreateUserSignatureModalAsync();
            await Message.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual bool ValidateCreateUserSignature()
    {
        CreateSignatureValidationErrorKey = null;
        CreateSignatureFieldErrors.Clear();
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(NewUserSignature?.SignType))
        {
            CreateSignatureFieldErrors["SignType"] = L["SignTypeRequired"];
            CreateSignatureValidationErrorKey = "SignTypeRequired";
            isValid = false;
        }

        if (SelectedSignatureSettingForCreate == null || !SelectedSignatureSettingForCreate.Any())
        {
            CreateSignatureFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            if (isValid) CreateSignatureValidationErrorKey = "ProviderCodeRequired";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(NewUserSignature?.SignatureImage))
        {
            CreateSignatureFieldErrors["SignatureImage"] = L["SignatureImageRequired"];
            if (isValid) CreateSignatureValidationErrorKey = "SignatureImageRequired";
            isValid = false;
        }

        if (IsDigitalSignType(NewUserSignature?.SignType))
        {
            if (string.IsNullOrWhiteSpace(NewUserSignature?.TokenRef))
            {
                CreateSignatureFieldErrors["TokenRef"] = L["TokenRefRequiredForDigitalSign"];
                if (isValid) CreateSignatureValidationErrorKey = "TokenRefRequiredForDigitalSign";
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(NewUserSignature?.Secret))
            {
                CreateSignatureFieldErrors["Secret"] = L["SecretRequiredForDigitalSign"];
                if (isValid) CreateSignatureValidationErrorKey = "SecretRequiredForDigitalSign";
                isValid = false;
            }

            if (!IsRemoteCaSelectedSignatureSetting(SelectedSignatureSettingForCreate) && string.IsNullOrWhiteSpace(NewUserSignature?.SealImg))
            {
                CreateSignatureFieldErrors["SealImg"] = L["SealImgRequiredForDigitalSign"];
                if (isValid) CreateSignatureValidationErrorKey = "SealImgRequiredForDigitalSign";
                isValid = false;
            }
        }

        return isValid;
    }

    protected virtual async Task OpenEditUserSignatureModalAsync(UserSignatureWithNavigationPropertiesDto input)
    {
        var userSignature = await UserSignaturesAppService.GetWithNavigationPropertiesAsync(input.UserSignature.Id);
        EditingUserSignatureId = userSignature.UserSignature.Id;
        EditingUserSignature = ObjectMapper.Map<UserSignatureDto, UserSignatureUpdateDto>(userSignature.UserSignature);
        EditSignatureValidationErrorKey = null;
        EditSignatureFieldErrors.Clear();
        
        await LoadSignatureSettingsLookupAsync(signType: EditingUserSignature.SignType);
        
        // Set selected signature setting for Select2
        var signatureSettingId = SignatureSettingsIdToCodeMap
            .FirstOrDefault(x => x.Value == EditingUserSignature.ProviderCode).Key;
        
        if (signatureSettingId != Guid.Empty)
        {
            SelectedSignatureSettingForEdit = new List<LookupDto<Guid>>
            {
                new LookupDto<Guid>
                {
                    Id = signatureSettingId,
                    DisplayName = EditingUserSignature.ProviderCode
                }
            };
        }
        else
        {
            SelectedSignatureSettingForEdit.Clear();
        }
        
        await EditUserSignatureModal.Show();
    }

    protected virtual async Task CloseEditUserSignatureModalAsync()
    {
        await EditUserSignatureModal.Hide();
    }

    protected virtual async Task UpdateUserSignatureAsync()
    {
        try
        {
            if (!ValidateEditUserSignature())
            {
                await UiMessageService.Warn(L[EditSignatureValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            // Map SignatureSettingId to ProviderCode
            if (SelectedSignatureSettingForEdit != null && SelectedSignatureSettingForEdit.Any())
            {
                var selectedId = SelectedSignatureSettingForEdit.First().Id;
                if (SignatureSettingsIdToCodeMap.ContainsKey(selectedId))
                {
                    EditingUserSignature.ProviderCode = SignatureSettingsIdToCodeMap[selectedId];
                }
            }

            ClearSealImageIfElectronicSignature(EditingUserSignature);

            await UserSignaturesAppService.UpdateAsync(EditingUserSignatureId, EditingUserSignature);
            await LoadUserSignaturesAsync();
            await EditUserSignatureModal.Hide();
            await Message.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual bool ValidateEditUserSignature()
    {
        EditSignatureValidationErrorKey = null;
        EditSignatureFieldErrors.Clear();
        bool isValid = true;

        if (string.IsNullOrWhiteSpace(EditingUserSignature?.SignType))
        {
            EditSignatureFieldErrors["SignType"] = L["SignTypeRequired"];
            EditSignatureValidationErrorKey = "SignTypeRequired";
            isValid = false;
        }

        if (SelectedSignatureSettingForEdit == null || !SelectedSignatureSettingForEdit.Any())
        {
            EditSignatureFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            if (isValid) EditSignatureValidationErrorKey = "ProviderCodeRequired";
            isValid = false;
        }

        if (string.IsNullOrWhiteSpace(EditingUserSignature?.SignatureImage))
        {
            EditSignatureFieldErrors["SignatureImage"] = L["SignatureImageRequired"];
            if (isValid) EditSignatureValidationErrorKey = "SignatureImageRequired";
            isValid = false;
        }

        if (IsDigitalSignType(EditingUserSignature?.SignType))
        {
            if (string.IsNullOrWhiteSpace(EditingUserSignature?.TokenRef))
            {
                EditSignatureFieldErrors["TokenRef"] = L["TokenRefRequiredForDigitalSign"];
                if (isValid) EditSignatureValidationErrorKey = "TokenRefRequiredForDigitalSign";
                isValid = false;
            }

            // Secret is not loaded on edit (API does not return it); leave blank to keep existing

            if (!IsRemoteCaSelectedSignatureSetting(SelectedSignatureSettingForEdit) && string.IsNullOrWhiteSpace(EditingUserSignature?.SealImg))
            {
                EditSignatureFieldErrors["SealImg"] = L["SealImgRequiredForDigitalSign"];
                if (isValid) EditSignatureValidationErrorKey = "SealImgRequiredForDigitalSign";
                isValid = false;
            }
        }

        return isValid;
    }

    protected virtual async Task DeleteUserSignatureAsync(UserSignatureWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value,
        options: new Action<UiMessageOptions>(options => options.ConfirmButtonText = L["Confirm"])))
        {
            try
            {
                await UserSignaturesAppService.DeleteAsync(input.UserSignature.Id);
                await LoadUserSignaturesAsync();
                await Message.Success(L["SuccessfullyDeleted"]);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex);
            }
        }
    }

    // Helper methods for UserSignature field errors
    protected string? GetCreateSignatureFieldError(string fieldName) => CreateSignatureFieldErrors.GetValueOrDefault(fieldName);
    protected string? GetEditSignatureFieldError(string fieldName) => EditSignatureFieldErrors.GetValueOrDefault(fieldName);
    protected bool HasCreateSignatureFieldError(string fieldName) => CreateSignatureFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateSignatureFieldErrors[fieldName]);
    protected bool HasEditSignatureFieldError(string fieldName) => EditSignatureFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditSignatureFieldErrors[fieldName]);

    // Enum conversion helpers
    protected SignType? NewSignType
    {
        get => Enum.TryParse<SignType>(NewUserSignature.SignType, out var result) ? result : null;
        set => NewUserSignature.SignType = value?.ToString() ?? string.Empty;
    }

    protected SignType? EditingSignType
    {
        get => Enum.TryParse<SignType>(EditingUserSignature.SignType, out var result) ? result : null;
        set => EditingUserSignature.SignType = value?.ToString() ?? string.Empty;
    }

    protected virtual async Task OnCreateSignTypeChangedAsync(SignType? value)
    {
        NewSignType = value;
        CreateSignatureFieldErrors.Remove("SignType");
        SelectedSignatureSettingForCreate.Clear();
        if (!IsDigitalSignType(value?.ToString()))
        {
            NewUserSignature.SealImg = string.Empty;
        }
        await LoadSignatureSettingsLookupAsync(signType: value?.ToString());
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task OnEditSignTypeChangedAsync(SignType? value)
    {
        EditingSignType = value;
        EditSignatureFieldErrors.Remove("SignType");
        SelectedSignatureSettingForEdit.Clear();
        if (!IsDigitalSignType(value?.ToString()))
        {
            EditingUserSignature.SealImg = string.Empty;
        }
        await LoadSignatureSettingsLookupAsync(signType: value?.ToString());
        await InvokeAsync(StateHasChanged);
    }

    private static bool IsDigitalSignType(string? signType)
    {
        return string.Equals(signType, nameof(SignType.DIGITAL), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Electronic signatures do not use seal image; strip any stale path (e.g. after switching from DIGITAL).
    /// </summary>
    private static void ClearSealImageIfElectronicSignature(UserSignatureCreateDto dto)
    {
        if (!IsDigitalSignType(dto.SignType))
        {
            dto.SealImg = string.Empty;
        }
    }

    private static void ClearSealImageIfElectronicSignature(UserSignatureUpdateDto dto)
    {
        if (!IsDigitalSignType(dto.SignType))
        {
            dto.SealImg = string.Empty;
        }
    }

    // Avatar Upload Methods
    protected virtual async Task TriggerAvatarUploadAsync()
    {
        await JSRuntime.InvokeVoidAsync("eval", "document.getElementById('avatarFileInput').click()");
    }

    protected virtual async Task HandleAvatarUploadAsync(InputFileChangeEventArgs e)
    {
        try
        {
            var file = e.File;
            if (file != null)
            {
                const long maxFileSize = 5 * 1024 * 1024; // 5MB
                if (file.Size > maxFileSize)
                {
                    await Message.Error(L["FileTooLarge"]);
                    return;
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var extension = System.IO.Path.GetExtension(file.Name).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                {
                    await Message.Error(L["InvalidFileType"]);
                    return;
                }

                // Show loading message
                await Message.Info(L["UploadingAvatar"]);

                // Read file content
                using var memoryStream = new System.IO.MemoryStream();
                using var stream = file.OpenReadStream(maxFileSize);
                await stream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                // Upload to server
                await ProfileAppService.SetProfilePictureAsync(new ProfilePictureInput
                {
                    Type = ProfilePictureType.Image,
                    ImageContent = new RemoteStreamContent(memoryStream, file.Name, file.ContentType)
                });
                
                // Show success message
                await Message.Success(L["AvatarUploadedSuccessfully"]);
                
                // Reload page to show new avatar
                await JSRuntime.InvokeVoidAsync("location.reload");
            }
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    // Signature Image Upload Methods
    protected virtual async Task OnCreateSignatureImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files == null || !e.Files.Any())
        {
            NewUserSignature.SignatureImage = string.Empty;
            ResetSignatureImageUploadState();
            CreateSignatureFieldErrors.Remove("SignatureImage");
            await InvokeAsync(StateHasChanged);
            return;
        }

        await UploadSignatureImageFileAsync(e.Files.First(), isEditMode: false);
    }

    protected virtual async Task OnEditSignatureImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files == null || !e.Files.Any())
        {
            EditingUserSignature.SignatureImage = string.Empty;
            ResetSignatureImageUploadState();
            EditSignatureFieldErrors.Remove("SignatureImage");
            await InvokeAsync(StateHasChanged);
            return;
        }

        await UploadSignatureImageFileAsync(e.Files.First(), isEditMode: true);
    }

    protected virtual async Task OnCreateSealImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files == null || !e.Files.Any())
        {
            NewUserSignature.SealImg = string.Empty;
            CreateSignatureFieldErrors.Remove("SealImg");
            await InvokeAsync(StateHasChanged);
            return;
        }

        await UploadSealImageFileAsync(e.Files.First(), isEditMode: false);
    }

    protected virtual async Task OnEditSealImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files == null || !e.Files.Any())
        {
            EditingUserSignature.SealImg = string.Empty;
            EditSignatureFieldErrors.Remove("SealImg");
            await InvokeAsync(StateHasChanged);
            return;
        }

        await UploadSealImageFileAsync(e.Files.First(), isEditMode: true);
    }

    protected virtual async Task UploadSignatureImageFileAsync(IFileEntry file, bool isEditMode)
    {
        try
        {
            // Validate file type FIRST before setting any state
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();

            if (!allowedExtensions.Contains(fileExtension))
            {
                await Message.Error(L["OnlyImageFilesAllowed"]);
                // Clear the file picker to remove the invalid file from UI
                if (isEditMode)
                {
                    await EditSignatureImageFilePicker.Clear();
                }
                else
                {
                    await CreateSignatureImageFilePicker.Clear();
                }
                return;
            }

            // Validate file size (50MB)
            if (file.Size > 52428800)
            {
                await Message.Error(L["FileSizeTooLarge"]);
                // Clear the file picker to remove the invalid file from UI
                if (isEditMode)
                {
                    await EditSignatureImageFilePicker.Clear();
                }
                else
                {
                    await CreateSignatureImageFilePicker.Clear();
                }
                return;
            }

            // Set uploading state AFTER validation passes
            IsUploadingSignatureImage = true;
            SelectedSignatureImageFile = file;
            SignatureImageFilePickerProgress = 0;

            // Read file content
            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var fileBytes = memoryStream.ToArray();

            // Generate unique file name (sanitize to avoid oversized object keys)
            var safeFileName = BlobStoragePathHelper.SanitizeFileName(file.Name);
            var fileName = $"{Guid.NewGuid()}_{safeFileName}";
            var filePath = $"user-signature-images/{fileName}";

            // Upload to blob storage (overwrite if re-uploading same path)
            await BlobContainer.SaveAsync(filePath, fileBytes, overrideExisting: true);

            // Update state based on mode
            UploadedSignatureImagePath = filePath;
            if (isEditMode)
            {
                EditingUserSignature.SignatureImage = filePath;
                EditSignatureFieldErrors.Remove("SignatureImage");
            }
            else
            {
                NewUserSignature.SignatureImage = filePath;
                CreateSignatureFieldErrors.Remove("SignatureImage");
            }
            SignatureImageFilePickerProgress = 100;

            await Message.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
            ResetSignatureImageUploadState();
        }
        finally
        {
            IsUploadingSignatureImage = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual void ResetSignatureImageUploadState()
    {
        SelectedSignatureImageFile = null;
        UploadedSignatureImagePath = string.Empty;
        SignatureImageFilePickerProgress = 0;
        IsUploadingSignatureImage = false;
    }

    protected virtual async Task UploadSealImageFileAsync(IFileEntry file, bool isEditMode)
    {
        try
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                await Message.Error(L["OnlyImageFilesAllowed"]);
                if (isEditMode)
                {
                    await EditSealImageFilePicker.Clear();
                }
                else
                {
                    await CreateSealImageFilePicker.Clear();
                }
                return;
            }

            if (file.Size > 52428800)
            {
                await Message.Error(L["FileSizeTooLarge"]);
                if (isEditMode)
                {
                    await EditSealImageFilePicker.Clear();
                }
                else
                {
                    await CreateSealImageFilePicker.Clear();
                }
                return;
            }

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            var safeFileName = BlobStoragePathHelper.SanitizeFileName(file.Name);
            var filePath = $"user-seal-images/{Guid.NewGuid()}_{safeFileName}";
            await BlobContainer.SaveAsync(filePath, memoryStream.ToArray(), overrideExisting: true);

            if (isEditMode)
            {
                EditingUserSignature.SealImg = filePath;
                EditSignatureFieldErrors.Remove("SealImg");
            }
            else
            {
                NewUserSignature.SealImg = filePath;
                CreateSignatureFieldErrors.Remove("SealImg");
            }

            await Message.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual string GetSignatureImageUrl(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return string.Empty;
        }

        return BlobDisplayUrlProvider.GetDisplayUrl(imagePath);
    }

    // Signature Settings Lookup Methods
    protected virtual async Task LoadSignatureSettingsLookupAsync(string? filterText = null, string? signType = null)
    {
        PagedResultDto<LookupDto<Guid>> result;
        if (!string.IsNullOrWhiteSpace(signType))
        {
            result = await SignatureSettingsAppService.GetSignatureSettingLookupBySignTypeAsync(
                new GetSignatureSettingLookupBySignTypeInput
                {
                    Filter = filterText,
                    DefaultSignType = signType
                });
        }
        else
        {
            result = await SignatureSettingsAppService.GetSignatureSettingLookupAsync(new LookupRequestDto { Filter = filterText });
        }

        SignatureSettingsCollection = result.Items;
        
        SignatureSettingsIdToProviderTypeMap.Clear();
        if (string.Equals(signType, nameof(SignType.DIGITAL), StringComparison.OrdinalIgnoreCase))
        {
            var settingsPage = await SignatureSettingsAppService.GetListAsync(new GetSignatureSettingsInput
            {
                MaxResultCount = 512,
                SkipCount = 0,
                AllowDigitalSign = true,
                IsActive = true,
                Sorting = "ProviderCode"
            });
            SignatureSettingsIdToProviderTypeMap = settingsPage.Items
                .GroupBy(x => x.Id)
                .ToDictionary(g => g.Key, g => g.First().ProviderType);
        }

        // Build mapper from SignatureSetting Id to ProviderCode
        SignatureSettingsIdToCodeMap.Clear();
        foreach (var item in SignatureSettingsCollection)
        {
            SignatureSettingsIdToCodeMap[item.Id] = item.DisplayName;
        }
    }

    protected virtual async Task<List<LookupDto<Guid>>> GetSignatureSettingsCollectionLookupForCreateAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        await LoadSignatureSettingsLookupAsync(filter, NewSignType?.ToString());
        return SignatureSettingsCollection.ToList();
    }

    protected virtual async Task<List<LookupDto<Guid>>> GetSignatureSettingsCollectionLookupForEditAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        await LoadSignatureSettingsLookupAsync(filter, EditingSignType?.ToString());
        return SignatureSettingsCollection.ToList();
    }

    protected virtual void OnSignatureSettingChangedForCreate()
    {
        CreateSignatureFieldErrors.Remove("ProviderCode");
    }

    protected virtual void OnSignatureSettingChangedForEdit()
    {
        EditSignatureFieldErrors.Remove("ProviderCode");
    }

    private bool IsRemoteCaSelectedSignatureSetting(IReadOnlyList<LookupDto<Guid>>? selection)
    {
        if (selection == null || selection.Count == 0)
        {
            return false;
        }

        var id = selection[0].Id;
        if (!SignatureSettingsIdToProviderTypeMap.TryGetValue(id, out var providerTypeStr))
        {
            return false;
        }

        return Enum.TryParse<ProviderType>(providerTypeStr ?? string.Empty, ignoreCase: true, out var parsed)
               && parsed == ProviderType.REMOTE_CA;
    }
}
