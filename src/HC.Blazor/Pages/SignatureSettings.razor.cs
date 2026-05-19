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
using Microsoft.Extensions.Logging;
using HC.Blazor.BlobStoring;
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
    private int CreateLayoutImgPickerKey { get; set; }
    private int EditLayoutImgPickerKey { get; set; }
    private bool IsUploadingLayoutImg { get; set; }

    [Inject]
    protected IBlobDisplayUrlProvider BlobDisplayUrlProvider { get; set; } = default!;

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

    private List<SignatureSettingDto> SelectedSignatureSettings { get; set; } = new();
    private bool AllSignatureSettingsSelected { get; set; }

    [Inject]
    protected ILogger<SignatureSettings> Logger { get; set; } = default!;

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
        CreateLayoutImgPickerKey++;
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
        CreateLayoutImgPickerKey++;
        await CreateSignatureSettingModal.Hide();
    }

    private async Task OpenEditSignatureSettingModalAsync(SignatureSettingDto input)
    {
        SelectedEditTab = "signatureSetting-edit-tab";
        var signatureSetting = await SignatureSettingsAppService.GetAsync(input.Id);
        EditingSignatureSettingId = signatureSetting.Id;
        EditingSignatureSetting = ObjectMapper.Map<SignatureSettingDto, SignatureSettingUpdateDto>(signatureSetting);
        EditLayoutImgPickerKey++;
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
            Logger.LogInformation(
                "Create SignatureSetting requested. ProviderCode={ProviderCode}, AllowDigitalSign={AllowDigitalSign}, LayoutImg={LayoutImg}",
                NewSignatureSetting.ProviderCode,
                NewSignatureSetting.AllowDigitalSign,
                NewSignatureSetting.LayoutImg
            );

            if (!ValidateCreateSignatureSetting())
            {
                Logger.LogWarning(
                    "Create SignatureSetting validation failed. ErrorKey={ErrorKey}, LayoutImg={LayoutImg}",
                    CreateSignatureSettingValidationErrorKey,
                    NewSignatureSetting.LayoutImg
                );

                await UiMessageService.Warn(L[CreateSignatureSettingValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SignatureSettingsAppService.CreateAsync(NewSignatureSetting);
            Logger.LogInformation(
                "Create SignatureSetting completed. ProviderCode={ProviderCode}, LayoutImg={LayoutImg}",
                NewSignatureSetting.ProviderCode,
                NewSignatureSetting.LayoutImg
            );
            await GetSignatureSettingsAsync();
            await CloseCreateSignatureSettingModalAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Create SignatureSetting failed. ProviderCode={ProviderCode}, LayoutImg={LayoutImg}",
                NewSignatureSetting.ProviderCode,
                NewSignatureSetting.LayoutImg
            );
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

        if (IsDigitalLayoutImageRequired(NewSignatureSetting.AllowDigitalSign, NewSignatureSetting.ProviderType))
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
        EditLayoutImgPickerKey++;
        await EditSignatureSettingModal.Hide();
    }

    private static bool IsRemoteCaSignatureProviderType(string? providerTypeString)
    {
        return Enum.TryParse<ProviderType>(providerTypeString ?? string.Empty, ignoreCase: true, out var pt)
               && pt == ProviderType.REMOTE_CA;
    }

    private bool IsDigitalLayoutImageRequired(bool allowDigitalSign, string? providerType)
    {
        return allowDigitalSign && !IsRemoteCaSignatureProviderType(providerType);
    }

    private async Task UpdateSignatureSettingAsync()
    {
        try
        {
            Logger.LogInformation(
                "Update SignatureSetting requested. Id={Id}, ProviderCode={ProviderCode}, AllowDigitalSign={AllowDigitalSign}, LayoutImg={LayoutImg}",
                EditingSignatureSettingId,
                EditingSignatureSetting.ProviderCode,
                EditingSignatureSetting.AllowDigitalSign,
                EditingSignatureSetting.LayoutImg
            );

            if (!ValidateEditSignatureSetting())
            {
                Logger.LogWarning(
                    "Update SignatureSetting validation failed. Id={Id}, ErrorKey={ErrorKey}, LayoutImg={LayoutImg}",
                    EditingSignatureSettingId,
                    EditSignatureSettingValidationErrorKey,
                    EditingSignatureSetting.LayoutImg
                );

                await UiMessageService.Warn(L[EditSignatureSettingValidationErrorKey ?? "ValidationError"],
                options: new Action<UiMessageOptions>(options => options.OkButtonText = L["Ok"]));
                await InvokeAsync(StateHasChanged);
                return;
            }

            await BlockUiService.Block(selectors: "#lpx-wrapper", busy: true);            
            await SignatureSettingsAppService.UpdateAsync(EditingSignatureSettingId, EditingSignatureSetting);
            Logger.LogInformation(
                "Update SignatureSetting completed. Id={Id}, LayoutImg={LayoutImg}",
                EditingSignatureSettingId,
                EditingSignatureSetting.LayoutImg
            );
            await GetSignatureSettingsAsync();
            await EditSignatureSettingModal.Hide();
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Update SignatureSetting failed. Id={Id}, LayoutImg={LayoutImg}",
                EditingSignatureSettingId,
                EditingSignatureSetting.LayoutImg
            );
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

        if (IsDigitalLayoutImageRequired(EditingSignatureSetting.AllowDigitalSign, EditingSignatureSetting.ProviderType))
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

    private string AllowElectronicSignFilterValue { get; set; } = string.Empty;
    private string AllowDigitalSignFilterValue { get; set; } = string.Empty;
    private string RequireOtpFilterValue { get; set; } = string.Empty;

    protected virtual async Task OnAllowElectronicSignChangedAsync(string? allowElectronicSign)
    {
        Filter.AllowElectronicSign = allowElectronicSign switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        AllowElectronicSignFilterValue = allowElectronicSign ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnAllowDigitalSignChangedAsync(string? allowDigitalSign)
    {
        Filter.AllowDigitalSign = allowDigitalSign switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        AllowDigitalSignFilterValue = allowDigitalSign ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnRequireOtpChangedAsync(string? requireOtp)
    {
        Filter.RequireOtp = requireOtp switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        RequireOtpFilterValue = requireOtp ?? string.Empty;
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

    private string KeepOriginalFileFilterValue { get; set; } = string.Empty;
    private string OverwriteSignedFileFilterValue { get; set; } = string.Empty;
    private string EnableSignLogFilterValue { get; set; } = string.Empty;

    protected virtual async Task OnKeepOriginalFileChangedAsync(string? keepOriginalFile)
    {
        Filter.KeepOriginalFile = keepOriginalFile switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        KeepOriginalFileFilterValue = keepOriginalFile ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnOverwriteSignedFileChangedAsync(string? overwriteSignedFile)
    {
        Filter.OverwriteSignedFile = overwriteSignedFile switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        OverwriteSignedFileFilterValue = overwriteSignedFile ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnEnableSignLogChangedAsync(string? enableSignLog)
    {
        Filter.EnableSignLog = enableSignLog switch { "True" or "true" => true, "False" or "false" => false, _ => null };
        EnableSignLogFilterValue = enableSignLog ?? string.Empty;
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

    // Use string for filter display - "All" (empty) works correctly (same pattern as IsActive)
    private string ProviderTypeFilterValue { get; set; } = string.Empty;
    private string DefaultSignTypeFilterValue { get; set; } = string.Empty;

    private async Task OnFilterProviderTypeChangedAsync(string? value)
    {
        Filter.ProviderType = string.IsNullOrEmpty(value) ? null : value;
        ProviderTypeFilterValue = value ?? string.Empty;
        await SearchAsync();
    }

    private async Task OnFilterDefaultSignTypeChangedAsync(string? value)
    {
        Filter.DefaultSignType = string.IsNullOrEmpty(value) ? null : value;
        DefaultSignTypeFilterValue = value ?? string.Empty;
        await SearchAsync();
    }

    protected virtual async Task OnCreateLayoutImgFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var selectedFile = e.Files.First();
            Logger.LogInformation(
                "Create layout image selected. FileName={FileName}, Size={Size}",
                selectedFile.Name,
                selectedFile.Size
            );
            await UploadLayoutImgFileAsync(e.Files.First(), false);
            return;
        }

        Logger.LogInformation("Create layout image cleared.");
        NewSignatureSetting.LayoutImg = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task OnEditLayoutImgFileChanged(FileChangedEventArgs e)
    {
        if (e.Files != null && e.Files.Any())
        {
            var selectedFile = e.Files.First();
            Logger.LogInformation(
                "Edit layout image selected. Id={Id}, FileName={FileName}, Size={Size}",
                EditingSignatureSettingId,
                selectedFile.Name,
                selectedFile.Size
            );
            await UploadLayoutImgFileAsync(e.Files.First(), true);
            return;
        }

        Logger.LogInformation("Edit layout image cleared. Id={Id}", EditingSignatureSettingId);
        EditingSignatureSetting.LayoutImg = string.Empty;
        await InvokeAsync(StateHasChanged);
    }

    protected virtual async Task UploadLayoutImgFileAsync(IFileEntry file, bool isEditMode)
    {
        try
        {
            Logger.LogInformation(
                "Upload layout image started. Mode={Mode}, FileName={FileName}, Size={Size}",
                isEditMode ? "Edit" : "Create",
                file.Name,
                file.Size
            );

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                Logger.LogWarning(
                    "Upload layout image rejected by extension. Mode={Mode}, FileName={FileName}, Extension={Extension}",
                    isEditMode ? "Edit" : "Create",
                    file.Name,
                    fileExtension
                );
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
                Logger.LogWarning(
                    "Upload layout image rejected by size. Mode={Mode}, FileName={FileName}, Size={Size}",
                    isEditMode ? "Edit" : "Create",
                    file.Name,
                    file.Size
                );
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
            Logger.LogInformation(
                "Saving layout image to blob. Mode={Mode}, BlobPath={BlobPath}, Bytes={Bytes}",
                isEditMode ? "Edit" : "Create",
                filePath,
                memoryStream.Length
            );
            await BlobContainer.SaveAsync(filePath, memoryStream.ToArray());
            Logger.LogInformation(
                "Saved layout image to blob successfully. Mode={Mode}, BlobPath={BlobPath}",
                isEditMode ? "Edit" : "Create",
                filePath
            );

            if (isEditMode)
            {
                EditingSignatureSetting.LayoutImg = filePath;
                EditFieldErrors.Remove("LayoutImg");
                Logger.LogInformation(
                    "Assigned layout image to edit model. Id={Id}, LayoutImg={LayoutImg}",
                    EditingSignatureSettingId,
                    EditingSignatureSetting.LayoutImg
                );
            }
            else
            {
                NewSignatureSetting.LayoutImg = filePath;
                CreateFieldErrors.Remove("LayoutImg");
                Logger.LogInformation(
                    "Assigned layout image to create model. ProviderCode={ProviderCode}, LayoutImg={LayoutImg}",
                    NewSignatureSetting.ProviderCode,
                    NewSignatureSetting.LayoutImg
                );
            }

            await Message.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Upload layout image failed. Mode={Mode}, FileName={FileName}",
                isEditMode ? "Edit" : "Create",
                file.Name
            );
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

        return BlobDisplayUrlProvider.GetDisplayUrl(imagePath);
    }
}