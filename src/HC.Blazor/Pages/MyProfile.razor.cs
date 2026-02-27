using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Volo.Abp.AuditLogging;
using Volo.Abp.AuditLogging.Blazor.Pages.Shared.AverageExecutionDurationPerDayWidget;
using Volo.Abp.AuditLogging.Blazor.Pages.Shared.ErrorRateWidget;
using Volo.Abp.Authorization.Permissions;
using Volo.Abp.BlazoriseUI;
using Volo.Abp.Identity;
using Volo.Saas.Host;
using Volo.Saas.Host.Blazor.Pages.Shared.Components.SaasEditionPercentageWidget;
using Volo.Saas.Host.Blazor.Pages.Shared.Components.SaasLatestTenantsWidget;
using HC.UserDepartments;
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
using Volo.Abp.Http.Client;
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
    public IUserDepartmentsAppService UserDepartmentsAppService { get; set; } = default!;

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
    public IRemoteServiceConfigurationProvider RemoteServiceConfigurationProvider { get; set; } = default!;

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

    protected List<UserDepartmentWithNavigationPropertiesDto> UserDepartments { get; set; } = new();

    protected List<UserSignatureWithNavigationPropertiesDto> UserSignatures { get; set; } = new();
    protected UserSignatureCreateDto NewUserSignature { get; set; } = new();
    protected UserSignatureUpdateDto EditingUserSignature { get; set; } = new();
    protected Guid EditingUserSignatureId { get; set; }
    protected Modal CreateUserSignatureModal { get; set; } = new();
    protected Modal EditUserSignatureModal { get; set; } = new();
    
    // Signature Settings Lookup
    protected IReadOnlyList<LookupDto<Guid>> SignatureSettingsCollection { get; set; } = new List<LookupDto<Guid>>();
    protected Dictionary<Guid, string> SignatureSettingsIdToCodeMap { get; set; } = new Dictionary<Guid, string>();
    protected List<LookupDto<Guid>> SelectedSignatureSettingForCreate { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedSignatureSettingForEdit { get; set; } = new();
    
    protected Dictionary<string, string?> CreateSignatureFieldErrors { get; set; } = new();
    protected Dictionary<string, string?> EditSignatureFieldErrors { get; set; } = new();
    protected string? CreateSignatureValidationErrorKey { get; set; }
    protected string? EditSignatureValidationErrorKey { get; set; }

    // File upload for signature image
    protected FilePicker CreateSignatureImageFilePicker { get; set; } = new();
    protected FilePicker EditSignatureImageFilePicker { get; set; } = new();
    protected IFileEntry? SelectedSignatureImageFile { get; set; }
    protected string UploadedSignatureImagePath { get; set; } = string.Empty;
    protected bool IsUploadingSignatureImage { get; set; }
    protected int SignatureImageFilePickerProgress { get; set; }
    
    protected string? _apiBaseUrl;

    // Department properties
    protected UserDepartmentCreateDto NewUserDepartment { get; set; } = new();
    protected UserDepartmentUpdateDto EditingUserDepartment { get; set; } = new();
    protected Guid EditingUserDepartmentId { get; set; }
    protected Modal CreateUserDepartmentModal { get; set; } = new();
    protected Modal EditUserDepartmentModal { get; set; } = new();
    protected IReadOnlyList<LookupDto<Guid>> DepartmentsCollection { get; set; } = new List<LookupDto<Guid>>();
    protected List<LookupDto<Guid>> SelectedDepartmentForCreate { get; set; } = new();
    protected List<LookupDto<Guid>> SelectedDepartmentForEdit { get; set; } = new();

    // Avatar upload
    protected string AvatarUrl { get; set; } = string.Empty;
    
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
    protected async override Task OnInitializedAsync()
    {
        StartDate = Clock.Now.AddMonths(-1).Date;
        EndDate = Clock.Now.Date;
        HasAuditLoggingPermission = await PermissionChecker.IsGrantedAsync(AbpAuditLoggingPermissions.AuditLogs.Default);
        HasSaasPermission = await PermissionChecker.IsGrantedAsync(SaasHostPermissions.Tenants.Default);

        // Load API base URL for image display
        var blobFilesService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("BlobFiles");

        _apiBaseUrl = blobFilesService?.BaseUrl?.EnsureEndsWith('/') ?? string.Empty;

        await LoadUserProfileAsync();
        await LoadUserDepartmentsAsync();
        await LoadUserSignaturesAsync();
        await LoadSignatureSettingsLookupAsync();
    }

    protected virtual async Task LoadUserProfileAsync()
    {
        if (CurrentUser.Id.HasValue)
        {
            var user = await IdentityUserAppService.GetAsync(CurrentUser.Id.Value);
            if (user != null)
            {
                ProfileModel = ObjectMapper.Map<IdentityUserDto, IdentityUserUpdateDto>(user);
            }
        }
    }

    protected virtual async Task LoadUserDepartmentsAsync()
    {
        if (CurrentUser.Id.HasValue)
        {
            var result = await UserDepartmentsAppService.GetListAsync(new GetUserDepartmentsInput
            {
                UserId = CurrentUser.Id.Value,
                MaxResultCount = 100,
                SkipCount = 0,
                Sorting = string.Empty
            });
            UserDepartments = result.Items.ToList();
        }   
    }

    protected virtual async Task SaveProfileAsync()
    {
        
        if (CurrentUser.Id.HasValue)
        {
            try{
                await IdentityUserAppService.UpdateAsync(CurrentUser.Id.Value, ProfileModel);
                await Message.Success(L["SuccessfullySaved"]);
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

        if (CurrentUser.Id.HasValue)
        {
            await IdentityUserAppService.UpdatePasswordAsync(CurrentUser.Id.Value, new IdentityUserUpdatePasswordInput
            {
                NewPassword = NewPassword
            });

            CurrentPassword = string.Empty;
            NewPassword = string.Empty;
            ConfirmNewPassword = string.Empty;

            await Message.Success(L["PasswordChangedSuccessfully"]);
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
        await LoadSignatureSettingsLookupAsync();
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
        
        await LoadSignatureSettingsLookupAsync();
        
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

            if (string.IsNullOrWhiteSpace(EditingUserSignature?.Secret))
            {
                EditSignatureFieldErrors["Secret"] = L["SecretRequiredForDigitalSign"];
                if (isValid) EditSignatureValidationErrorKey = "SecretRequiredForDigitalSign";
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

    private static bool IsDigitalSignType(string? signType)
    {
        return string.Equals(signType, nameof(SignType.DIGITAL), StringComparison.OrdinalIgnoreCase);
    }

    // Department Methods
    protected virtual async Task LoadDepartmentLookupAsync(string? filterText = null)
    {
        DepartmentsCollection = (await UserDepartmentsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = filterText })).Items;
    }

    protected virtual async Task<List<LookupDto<Guid>>> GetDepartmentCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        DepartmentsCollection = (await UserDepartmentsAppService.GetDepartmentLookupAsync(new LookupRequestDto { Filter = filter })).Items;
        return DepartmentsCollection.ToList();
    }

    protected virtual void OnDepartmentChangedForCreate()
    {
        if (SelectedDepartmentForCreate != null && SelectedDepartmentForCreate.Any())
        {
            NewUserDepartment.DepartmentId = SelectedDepartmentForCreate.First().Id;
        }
    }

    protected virtual void OnDepartmentChangedForEdit()
    {
        if (SelectedDepartmentForEdit != null && SelectedDepartmentForEdit.Any())
        {
            EditingUserDepartment.DepartmentId = SelectedDepartmentForEdit.First().Id;
        }
    }

    protected virtual async Task OpenCreateUserDepartmentModalAsync()
    {
        NewUserDepartment = new UserDepartmentCreateDto
        {
            UserId = CurrentUser.Id ?? Guid.Empty,
            IsPrimary = false,
            IsActive = true
        };
        await LoadDepartmentLookupAsync();
        await CreateUserDepartmentModal.Show();
    }

    protected virtual async Task CloseCreateUserDepartmentModalAsync()
    {
        await CreateUserDepartmentModal.Hide();
    }

    protected virtual async Task CreateUserDepartmentAsync()
    {
        try
        {
            if (CurrentUser.Id.HasValue)
            {
                NewUserDepartment.UserId = CurrentUser.Id.Value;
            }

            await UserDepartmentsAppService.CreateAsync(NewUserDepartment);
            await LoadUserDepartmentsAsync();
            await CloseCreateUserDepartmentModalAsync();
            await Message.Success(L["SuccessfullySaved"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual async Task OpenEditUserDepartmentModalAsync(UserDepartmentWithNavigationPropertiesDto input)
    {
        var userDepartment = await UserDepartmentsAppService.GetWithNavigationPropertiesAsync(input.UserDepartment.Id);
        EditingUserDepartmentId = userDepartment.UserDepartment.Id;
        EditingUserDepartment = ObjectMapper.Map<UserDepartmentDto, UserDepartmentUpdateDto>(userDepartment.UserDepartment);
        await LoadDepartmentLookupAsync(userDepartment.Department.Name);
        
        // Set selected department for Select2
        SelectedDepartmentForEdit = new List<LookupDto<Guid>>
        {
            new LookupDto<Guid>
            {
                Id = userDepartment.Department.Id,
                DisplayName = userDepartment.Department.Name
            }
        };
        
        await EditUserDepartmentModal.Show();
    }

    protected virtual async Task CloseEditUserDepartmentModalAsync()
    {
        await EditUserDepartmentModal.Hide();
    }

    protected virtual async Task UpdateUserDepartmentAsync()
    {
        try
        {
            await UserDepartmentsAppService.UpdateAsync(EditingUserDepartmentId, EditingUserDepartment);
            await LoadUserDepartmentsAsync();
            await EditUserDepartmentModal.Hide();
            await Message.Success(L["SuccessfullySaved"],
            options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
    }

    protected virtual async Task DeleteUserDepartmentAsync(UserDepartmentWithNavigationPropertiesDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            try
            {
                await UserDepartmentsAppService.DeleteAsync(input.UserDepartment.Id);
                await LoadUserDepartmentsAsync();
                await Message.Success(L["SuccessfullyDeleted"]);
            }
            catch (Exception ex)
            {
                await HandleErrorAsync(ex);
            }
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
        if (e.Files != null && e.Files.Any())
        {
            var file = e.Files.First();
            await UploadSignatureImageFileAsync(file, isEditMode: false);
        }
    }

    protected virtual async Task OnEditSignatureImageFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var file = e.Files.First();
            await UploadSignatureImageFileAsync(file, isEditMode: true);
        }
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

            // Generate unique file name
            var fileName = $"{Guid.NewGuid()}_{file.Name}";
            var filePath = $"user-signature-images/{fileName}";

            // Upload to blob storage
            await BlobContainer.SaveAsync(filePath, fileBytes);

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

    protected virtual string GetSignatureImageUrl(string imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
            return string.Empty;
            
        // Use cached API base URL or set default
        if (string.IsNullOrEmpty(_apiBaseUrl))
        {
            _apiBaseUrl = "/"; // Will be properly set in OnInitializedAsync
        }
        
        return $"{_apiBaseUrl}api/app/blob-files/file?path={Uri.EscapeDataString(imagePath)}";
    }

    // Signature Settings Lookup Methods
    protected virtual async Task LoadSignatureSettingsLookupAsync(string? filterText = null)
    {
        var result = await SignatureSettingsAppService.GetSignatureSettingLookupAsync(new LookupRequestDto { Filter = filterText });
        SignatureSettingsCollection = result.Items;
        
        // Build mapper from SignatureSetting Id to ProviderCode
        SignatureSettingsIdToCodeMap.Clear();
        foreach (var item in SignatureSettingsCollection)
        {
            SignatureSettingsIdToCodeMap[item.Id] = item.DisplayName;
        }
    }

    protected virtual async Task<List<LookupDto<Guid>>> GetSignatureSettingsCollectionLookupAsync(IReadOnlyList<LookupDto<Guid>> dbset, string filter, CancellationToken token)
    {
        await LoadSignatureSettingsLookupAsync(filter);
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
}
