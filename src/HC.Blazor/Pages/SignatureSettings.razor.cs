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
using HC.SignatureSettings;
using HC.Permissions;
using HC.Shared;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Volo.Abp;
using Volo.Abp.Content;
using Volo.Abp.AspNetCore.Components.Messages;
using Volo.Abp.BlobStoring;
namespace HC.Blazor.Pages;

public partial class SignatureSettings : HCComponentBase
{
    protected List<Volo.Abp.BlazoriseUI.BreadcrumbItem> BreadcrumbItems = new List<Volo.Abp.BlazoriseUI.BreadcrumbItem>();

    protected PageToolbar Toolbar { get; } = new PageToolbar();
    protected bool ShowAdvancedFilters { get; set; }

    public DataGrid<SignatureSettingDto>? DataGridRef { get; set; }

    private IReadOnlyList<SignatureSettingDto> SignatureSettingList { get; set; }

    private int PageSize { get; } = LimitedResultRequestDto.DefaultMaxResultCount;
    private int CurrentPage { get; set; } = 1;
    private string CurrentSorting { get; set; } = string.Empty;
    private int TotalCount { get; set; }

    private bool CanCreateSignatureSetting { get; set; }

    private bool CanEditSignatureSetting { get; set; }

    private bool CanDeleteSignatureSetting { get; set; }

    private SignatureSettingCreateDto NewSignatureSetting { get; set; }

    private SignatureSettingUpdateDto EditingSignatureSetting { get; set; }
    private FilePicker CreateLayoutImgFilePicker { get; set; } = new();
    private FilePicker EditLayoutImgFilePicker { get; set; } = new();
    private bool IsUploadingLayoutImg { get; set; }
    private string? _apiBaseUrl;

    // Field-level validation errors
    private Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    private Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    // Validation error keys
    private string? CreateSignatureSettingValidationErrorKey { get; set; }
    private string? EditSignatureSettingValidationErrorKey { get; set; }

    // Helper methods to get field errors
    private string? GetCreateFieldError(string fieldName) => CreateFieldErrors.GetValueOrDefault(fieldName);
    private string? GetEditFieldError(string fieldName) => EditFieldErrors.GetValueOrDefault(fieldName);
    private bool HasCreateFieldError(string fieldName) => CreateFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(CreateFieldErrors[fieldName]);
    private bool HasEditFieldError(string fieldName) => EditFieldErrors.ContainsKey(fieldName) && !string.IsNullOrWhiteSpace(EditFieldErrors[fieldName]);
    private Guid EditingSignatureSettingId { get; set; }

    private Modal CreateSignatureSettingModal { get; set; } = new();
    private Modal EditSignatureSettingModal { get; set; } = new();
    private GetSignatureSettingsInput Filter { get; set; }

    private DataGridEntityActionsColumn<SignatureSettingDto> EntityActionsColumn { get; set; } = new();

    protected string SelectedCreateTab = "signatureSetting-create-tab";
    protected string SelectedEditTab = "signatureSetting-edit-tab";
    private SignatureSettingDto? SelectedSignatureSetting;

    private List<SignatureSettingDto> SelectedSignatureSettings { get; set; } = new();
    private bool AllSignatureSettingsSelected { get; set; }

    public SignatureSettings()
    {
        NewSignatureSetting = new SignatureSettingCreateDto();
        EditingSignatureSetting = new SignatureSettingUpdateDto();
        Filter = new GetSignatureSettingsInput
        {
            MaxResultCount = PageSize,
            SkipCount = (CurrentPage - 1) * PageSize,
            Sorting = CurrentSorting
        };
        SignatureSettingList = new List<SignatureSettingDto>();
    }

    protected override async Task OnInitializedAsync()
    {
        await SetPermissionsAsync();
        var blobFilesService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("BlobFiles");
        _apiBaseUrl = blobFilesService?.BaseUrl?.EnsureEndsWith('/') ?? string.Empty;
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
        BreadcrumbItems.Add(new Volo.Abp.BlazoriseUI.BreadcrumbItem(L["SignatureSettings"]));
        return ValueTask.CompletedTask;
    }

    protected virtual ValueTask SetToolbarItemsAsync()
    {
        Toolbar.AddButton(L["ExportToExcel"], async () => {
            await DownloadAsExcelAsync();
        }, IconName.Download);
        Toolbar.AddButton(L["NewSignatureSetting"], async () => {
            await OpenCreateSignatureSettingModalAsync();
        }, IconName.Add, requiredPolicyName: HCPermissions.MasterDatas.SignatureSettingsCreate);
        return ValueTask.CompletedTask;
    }

    private void ToggleDetails(SignatureSettingDto signatureSetting)
    {
        DataGridRef.ToggleDetailRow(signatureSetting, true);
    }

    private bool RowSelectableHandler(RowSelectableEventArgs<SignatureSettingDto> rowSelectableEventArgs) => rowSelectableEventArgs.SelectReason is not DataGridSelectReason.RowClick && CanDeleteSignatureSetting;

    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<SignatureSettingDto> detailRowTriggerEventArgs)
    {
        detailRowTriggerEventArgs.Toggleable = false;
        detailRowTriggerEventArgs.DetailRowTriggerType = DetailRowTriggerType.Manual;
        return true;
    }

    private async Task SetPermissionsAsync()
    {
        CanCreateSignatureSetting = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SignatureSettingsCreate);
        CanEditSignatureSetting = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SignatureSettingsEdit);
        CanDeleteSignatureSetting = await AuthorizationService.IsGrantedAsync(HCPermissions.MasterDatas.SignatureSettingsDelete);
    }

    private async Task GetSignatureSettingsAsync()
    {
        Filter.MaxResultCount = PageSize;
        Filter.SkipCount = (CurrentPage - 1) * PageSize;
        Filter.Sorting = CurrentSorting;
        var result = await SignatureSettingsAppService.GetListAsync(Filter);
        SignatureSettingList = result.Items;
        TotalCount = (int)result.TotalCount;
        await ClearSelection();
    }

    protected virtual async Task SearchAsync()
    {
        CurrentPage = 1;
        await GetSignatureSettingsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task DownloadAsExcelAsync()
    {
        var token = (await SignatureSettingsAppService.GetDownloadTokenAsync()).Token;
        var remoteService = await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("HC") ?? await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        var culture = CultureInfo.CurrentUICulture.Name ?? CultureInfo.CurrentCulture.Name;
        if (!culture.IsNullOrEmpty())
        {
            culture = "&culture=" + culture;
        }

        await RemoteServiceConfigurationProvider.GetConfigurationOrDefaultOrNullAsync("Default");
        NavigationManager.NavigateTo($"{remoteService?.BaseUrl.EnsureEndsWith('/') ?? string.Empty}api/app/signature-settings/as-excel-file?DownloadToken={token}&FilterText={HttpUtility.UrlEncode(Filter.FilterText)}{culture}&ProviderCode={HttpUtility.UrlEncode(Filter.ProviderCode)}&ProviderType={HttpUtility.UrlEncode(Filter.ProviderType?.ToString())}&ApiEndpoint={HttpUtility.UrlEncode(Filter.ApiEndpoint)}&ApiTimeoutMin={Filter.ApiTimeoutMin}&ApiTimeoutMax={Filter.ApiTimeoutMax}&DefaultSignType={HttpUtility.UrlEncode(Filter.DefaultSignType?.ToString())}&AllowElectronicSign={Filter.AllowElectronicSign}&AllowDigitalSign={Filter.AllowDigitalSign}&RequireOtp={Filter.RequireOtp}&SignWidthMin={Filter.SignWidthMin}&SignWidthMax={Filter.SignWidthMax}&SignHeightMin={Filter.SignHeightMin}&SignHeightMax={Filter.SignHeightMax}&SignedFileSuffix={HttpUtility.UrlEncode(Filter.SignedFileSuffix)}&KeepOriginalFile={Filter.KeepOriginalFile}&OverwriteSignedFile={Filter.OverwriteSignedFile}&EnableSignLog={Filter.EnableSignLog}&IsActive={Filter.IsActive}", forceLoad: true);
    }

    private async Task OnDataGridReadAsync(DataGridReadDataEventArgs<SignatureSettingDto> e)
    {
        CurrentSorting = e.Columns.Where(c => c.SortDirection != SortDirection.Default).Select(c => c.Field + (c.SortDirection == SortDirection.Descending ? " DESC" : "")).JoinAsString(",");
        CurrentPage = e.Page;
        await GetSignatureSettingsAsync();
        await InvokeAsync(StateHasChanged);
    }

    private async Task OpenCreateSignatureSettingModalAsync()
    {
        NewSignatureSetting = new SignatureSettingCreateDto
        {
        };
        SelectedCreateTab = "signatureSetting-create-tab";
        CreateSignatureSettingValidationErrorKey = null;
        CreateFieldErrors.Clear();
        await CreateSignatureSettingModal.Show();
    }

    private async Task CloseCreateSignatureSettingModalAsync()
    {
        NewSignatureSetting = new SignatureSettingCreateDto
        {
        };
        await CreateSignatureSettingModal.Hide();
    }

    private async Task OpenEditSignatureSettingModalAsync(SignatureSettingDto input)
    {
        SelectedEditTab = "signatureSetting-edit-tab";
        var signatureSetting = await SignatureSettingsAppService.GetAsync(input.Id);
        EditingSignatureSettingId = signatureSetting.Id;
        EditingSignatureSetting = ObjectMapper.Map<SignatureSettingDto, SignatureSettingUpdateDto>(signatureSetting);
        EditSignatureSettingValidationErrorKey = null;
        EditFieldErrors.Clear();
        await EditSignatureSettingModal.Show();
    }

    private async Task DeleteSignatureSettingAsync(SignatureSettingDto input)
    {
        try
        {
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SignatureSettingsAppService.DeleteAsync(input.Id);
            await GetSignatureSettingsAsync();
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

    private async Task DeleteSignatureSettingWithConfirmationAsync(SignatureSettingDto input)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await DeleteSignatureSettingAsync(input);
        }
    }

    private async Task CreateSignatureSettingAsync()
    {
        try
        {
            if (!ValidateCreateSignatureSetting())
            {
                await UiMessageService.Warn(L[CreateSignatureSettingValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SignatureSettingsAppService.CreateAsync(NewSignatureSetting);
            await GetSignatureSettingsAsync();
            await CloseCreateSignatureSettingModalAsync();
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

    private bool ValidateCreateSignatureSetting()
    {
        // Reset error state
        CreateSignatureSettingValidationErrorKey = null;
        CreateFieldErrors.Clear();

        bool isValid = true;

        // Required: ProviderCode
        if (string.IsNullOrWhiteSpace(NewSignatureSetting?.ProviderCode))
        {
            CreateFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            CreateSignatureSettingValidationErrorKey = "ProviderCodeRequired";
            isValid = false;
        }

        // Required: ProviderType
        if (string.IsNullOrWhiteSpace(NewSignatureSetting?.ProviderType))
        {
            CreateFieldErrors["ProviderType"] = L["ProviderTypeRequired"];
            if (isValid)
            {
                CreateSignatureSettingValidationErrorKey = "ProviderTypeRequired";
            }
            isValid = false;
        }

        // Required: ApiEndpoint
        if (string.IsNullOrWhiteSpace(NewSignatureSetting?.ApiEndpoint))
        {
            CreateFieldErrors["ApiEndpoint"] = L["ApiEndpointRequired"];
            if (isValid)
            {
                CreateSignatureSettingValidationErrorKey = "ApiEndpointRequired";
            }
            isValid = false;
        }

        // Required: DefaultSignType
        if (string.IsNullOrWhiteSpace(NewSignatureSetting?.DefaultSignType))
        {
            CreateFieldErrors["DefaultSignType"] = L["DefaultSignTypeRequired"];
            if (isValid)
            {
                CreateSignatureSettingValidationErrorKey = "DefaultSignTypeRequired";
            }
            isValid = false;
        }

        // Required: SignedFileSuffix
        if (string.IsNullOrWhiteSpace(NewSignatureSetting?.SignedFileSuffix))
        {
            CreateFieldErrors["SignedFileSuffix"] = L["SignedFileSuffixRequired"];
            if (isValid)
            {
                CreateSignatureSettingValidationErrorKey = "SignedFileSuffixRequired";
            }
            isValid = false;
        }

        if (NewSignatureSetting.AllowDigitalSign && string.IsNullOrWhiteSpace(NewSignatureSetting.LayoutImg))
        {
            CreateFieldErrors["LayoutImg"] = L["LayoutImgRequiredForDigitalSign"];
            if (isValid)
            {
                CreateSignatureSettingValidationErrorKey = "LayoutImgRequiredForDigitalSign";
            }
            isValid = false;
        }

        return isValid;
    }

    private async Task CloseEditSignatureSettingModalAsync()
    {
        await EditSignatureSettingModal.Hide();
    }

    private async Task UpdateSignatureSettingAsync()
    {
        try
        {
            if (!ValidateEditSignatureSetting())
            {
                await UiMessageService.Warn(L[EditSignatureSettingValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SignatureSettingsAppService.UpdateAsync(EditingSignatureSettingId, EditingSignatureSetting);
            await GetSignatureSettingsAsync();
            await EditSignatureSettingModal.Hide();
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

    private bool ValidateEditSignatureSetting()
    {
        // Reset error state
        EditSignatureSettingValidationErrorKey = null;
        EditFieldErrors.Clear();

        bool isValid = true;

        // Required: ProviderCode
        if (string.IsNullOrWhiteSpace(EditingSignatureSetting?.ProviderCode))
        {
            EditFieldErrors["ProviderCode"] = L["ProviderCodeRequired"];
            EditSignatureSettingValidationErrorKey = "ProviderCodeRequired";
            isValid = false;
        }

        // Required: ProviderType
        if (string.IsNullOrWhiteSpace(EditingSignatureSetting?.ProviderType))
        {
            EditFieldErrors["ProviderType"] = L["ProviderTypeRequired"];
            if (isValid)
            {
                EditSignatureSettingValidationErrorKey = "ProviderTypeRequired";
            }
            isValid = false;
        }

        // Required: ApiEndpoint
        if (string.IsNullOrWhiteSpace(EditingSignatureSetting?.ApiEndpoint))
        {
            EditFieldErrors["ApiEndpoint"] = L["ApiEndpointRequired"];
            if (isValid)
            {
                EditSignatureSettingValidationErrorKey = "ApiEndpointRequired";
            }
            isValid = false;
        }

        // Required: DefaultSignType
        if (string.IsNullOrWhiteSpace(EditingSignatureSetting?.DefaultSignType))
        {
            EditFieldErrors["DefaultSignType"] = L["DefaultSignTypeRequired"];
            if (isValid)
            {
                EditSignatureSettingValidationErrorKey = "DefaultSignTypeRequired";
            }
            isValid = false;
        }

        // Required: SignedFileSuffix
        if (string.IsNullOrWhiteSpace(EditingSignatureSetting?.SignedFileSuffix))
        {
            EditFieldErrors["SignedFileSuffix"] = L["SignedFileSuffixRequired"];
            if (isValid)
            {
                EditSignatureSettingValidationErrorKey = "SignedFileSuffixRequired";
            }
            isValid = false;
        }

        if (EditingSignatureSetting.AllowDigitalSign && string.IsNullOrWhiteSpace(EditingSignatureSetting.LayoutImg))
        {
            EditFieldErrors["LayoutImg"] = L["LayoutImgRequiredForDigitalSign"];
            if (isValid)
            {
                EditSignatureSettingValidationErrorKey = "LayoutImgRequiredForDigitalSign";
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

    protected virtual async Task OnProviderCodeChangedAsync(string? providerCode)
    {
        Filter.ProviderCode = providerCode;
        await SearchAsync();
    }

    protected virtual async Task OnProviderTypeChangedAsync(string? providerType)
    {
        Filter.ProviderType = providerType;
        await SearchAsync();
    }

    protected virtual async Task OnApiEndpointChangedAsync(string? apiEndpoint)
    {
        Filter.ApiEndpoint = apiEndpoint;
        await SearchAsync();
    }

    protected virtual async Task OnApiTimeoutMinChangedAsync(int? apiTimeoutMin)
    {
        Filter.ApiTimeoutMin = apiTimeoutMin;
        await SearchAsync();
    }

    protected virtual async Task OnApiTimeoutMaxChangedAsync(int? apiTimeoutMax)
    {
        Filter.ApiTimeoutMax = apiTimeoutMax;
        await SearchAsync();
    }

    protected virtual async Task OnDefaultSignTypeChangedAsync(string? defaultSignType)
    {
        Filter.DefaultSignType = defaultSignType;
        await SearchAsync();
    }

    protected virtual async Task OnAllowElectronicSignChangedAsync(bool? allowElectronicSign)
    {
        Filter.AllowElectronicSign = allowElectronicSign;
        await SearchAsync();
    }

    protected virtual async Task OnAllowDigitalSignChangedAsync(bool? allowDigitalSign)
    {
        Filter.AllowDigitalSign = allowDigitalSign;
        await SearchAsync();
    }

    protected virtual async Task OnRequireOtpChangedAsync(bool? requireOtp)
    {
        Filter.RequireOtp = requireOtp;
        await SearchAsync();
    }

    protected virtual async Task OnSignWidthMinChangedAsync(int? signWidthMin)
    {
        Filter.SignWidthMin = signWidthMin;
        await SearchAsync();
    }

    protected virtual async Task OnSignWidthMaxChangedAsync(int? signWidthMax)
    {
        Filter.SignWidthMax = signWidthMax;
        await SearchAsync();
    }

    protected virtual async Task OnSignHeightMinChangedAsync(int? signHeightMin)
    {
        Filter.SignHeightMin = signHeightMin;
        await SearchAsync();
    }

    protected virtual async Task OnSignHeightMaxChangedAsync(int? signHeightMax)
    {
        Filter.SignHeightMax = signHeightMax;
        await SearchAsync();
    }

    protected virtual async Task OnSignedFileSuffixChangedAsync(string? signedFileSuffix)
    {
        Filter.SignedFileSuffix = signedFileSuffix;
        await SearchAsync();
    }

    protected virtual async Task OnKeepOriginalFileChangedAsync(bool? keepOriginalFile)
    {
        Filter.KeepOriginalFile = keepOriginalFile;
        await SearchAsync();
    }

    protected virtual async Task OnOverwriteSignedFileChangedAsync(bool? overwriteSignedFile)
    {
        Filter.OverwriteSignedFile = overwriteSignedFile;
        await SearchAsync();
    }

    protected virtual async Task OnEnableSignLogChangedAsync(bool? enableSignLog)
    {
        Filter.EnableSignLog = enableSignLog;
        await SearchAsync();
    }

    protected virtual async Task OnIsActiveChangedAsync(bool? isActive)
    {
        Filter.IsActive = isActive;
        await SearchAsync();
    }

    private Task SelectAllItems()
    {
        AllSignatureSettingsSelected = true;
        return Task.CompletedTask;
    }

    private Task ClearSelection()
    {
        AllSignatureSettingsSelected = false;
        SelectedSignatureSettings.Clear();
        return Task.CompletedTask;
    }

    private Task SelectedSignatureSettingRowsChanged()
    {
        if (SelectedSignatureSettings.Count != PageSize)
        {
            AllSignatureSettingsSelected = false;
        }

        return Task.CompletedTask;
    }

    private async Task DeleteSelectedSignatureSettingsAsync()
    {
        var message = AllSignatureSettingsSelected ? L["DeleteAllRecords"].Value : L["DeleteSelectedRecords", SelectedSignatureSettings.Count].Value;
        if (!await UiMessageService.Confirm(message))
        {
            return;
        }

        try{        
            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            if (AllSignatureSettingsSelected)
            {
                await SignatureSettingsAppService.DeleteAllAsync(Filter);
            }
            else
            {
                await SignatureSettingsAppService.DeleteByIdsAsync(SelectedSignatureSettings.Select(x => x.Id).ToList());
            }

            SelectedSignatureSettings.Clear();
            AllSignatureSettingsSelected = false;            
            await GetSignatureSettingsAsync();            
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

    // Helper properties for enum conversion
    private ProviderType? NewProviderType
    {
        get => Enum.TryParse<ProviderType>(NewSignatureSetting.ProviderType, out var result) ? result : null;
        set => NewSignatureSetting.ProviderType = value?.ToString() ?? string.Empty;
    }

    private SignType? NewDefaultSignType
    {
        get => Enum.TryParse<SignType>(NewSignatureSetting.DefaultSignType, out var result) ? result : null;
        set => NewSignatureSetting.DefaultSignType = value?.ToString() ?? string.Empty;
    }

    private ProviderType? EditingProviderType
    {
        get => Enum.TryParse<ProviderType>(EditingSignatureSetting.ProviderType, out var result) ? result : null;
        set => EditingSignatureSetting.ProviderType = value?.ToString() ?? string.Empty;
    }

    private SignType? EditingDefaultSignType
    {
        get => Enum.TryParse<SignType>(EditingSignatureSetting.DefaultSignType, out var result) ? result : null;
        set => EditingSignatureSetting.DefaultSignType = value?.ToString() ?? string.Empty;
    }

    // Helper properties for filter enum conversion
    private ProviderType? FilterProviderType
    {
        get => Enum.TryParse<ProviderType>(Filter.ProviderType, out var result) ? result : null;
    }

    private async Task OnFilterProviderTypeChangedAsync(ProviderType? value)
    {
        Filter.ProviderType = value?.ToString();
        await OnProviderTypeChangedAsync(Filter.ProviderType);
    }

    private SignType? FilterDefaultSignType
    {
        get => Enum.TryParse<SignType>(Filter.DefaultSignType, out var result) ? result : null;
    }

    private async Task OnFilterDefaultSignTypeChangedAsync(SignType? value)
    {
        Filter.DefaultSignType = value?.ToString();
        await OnDefaultSignTypeChangedAsync(Filter.DefaultSignType);
    }

    protected virtual async Task OnCreateLayoutImgFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            await UploadLayoutImgFileAsync(e.Files.First(), false);
        }
    }

    protected virtual async Task OnEditLayoutImgFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            await UploadLayoutImgFileAsync(e.Files.First(), true);
        }
    }

    protected virtual async Task UploadLayoutImgFileAsync(IFileEntry file, bool isEditMode)
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
                    await EditLayoutImgFilePicker.Clear();
                }
                else
                {
                    await CreateLayoutImgFilePicker.Clear();
                }
                return;
            }

            if (file.Size > 52428800)
            {
                await Message.Error(L["FileSizeTooLarge"]);
                if (isEditMode)
                {
                    await EditLayoutImgFilePicker.Clear();
                }
                else
                {
                    await CreateLayoutImgFilePicker.Clear();
                }
                return;
            }

            IsUploadingLayoutImg = true;
            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            memoryStream.Position = 0;
            var filePath = $"signature-layout-images/{Guid.NewGuid()}_{file.Name}";
            await BlobContainer.SaveAsync(filePath, memoryStream.ToArray());

            if (isEditMode)
            {
                EditingSignatureSetting.LayoutImg = filePath;
                EditFieldErrors.Remove("LayoutImg");
            }
            else
            {
                NewSignatureSetting.LayoutImg = filePath;
                CreateFieldErrors.Remove("LayoutImg");
            }

            await Message.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await HandleErrorAsync(ex);
        }
        finally
        {
            IsUploadingLayoutImg = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    protected virtual string GetImageUrl(string? imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return string.Empty;
        }

        if (string.IsNullOrWhiteSpace(_apiBaseUrl))
        {
            _apiBaseUrl = "/";
        }

        return $"{_apiBaseUrl}api/app/blob-files/file?path={Uri.EscapeDataString(imagePath)}";
    }
}