using Blazorise;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HC.Blazor.BlobStoring;
using HC.BlobStoring;
using HC.SignatureSettings;
using HC.Shared;
using HC.UserSignatures;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Volo.Abp.Application.Dtos;
using Volo.Abp.BlobStoring;

namespace HC.Blazor.Components.UserSignaturesPanel;

public partial class UserSignaturesPanel
{
    [Parameter] public Guid IdentityUserId { get; set; }

    [Inject] public IUserSignaturesAppService UserSignaturesAppService { get; set; } = default!;
    [Inject] public ISignatureSettingsAppService SignatureSettingsAppService { get; set; } = default!;
    [Inject] public IBlobContainer BlobContainer { get; set; } = default!;
    [Inject] public IBlobDisplayUrlProvider BlobDisplayUrlProvider { get; set; } = default!;

    protected List<UserSignatureWithNavigationPropertiesDto> UserSignatures { get; set; } = new();

    protected Modal CreateModalRef { get; set; } = new();
    protected Modal EditModalRef { get; set; } = new();

    protected UserSignatureCreateDto CreateModel { get; set; } = new();
    protected UserSignatureUpdateDto EditModel { get; set; } = new();
    protected Guid EditingId { get; set; }

    protected List<LookupDto<Guid>> SignatureSettingsCollection { get; set; } = new();
    protected Dictionary<Guid, string> SignatureSettingsIdToCodeMap { get; set; } = new();
    protected Guid? CreateSignatureSettingId { get; set; }
    protected Guid? EditSignatureSettingId { get; set; }

    protected FilePicker? CreateSignatureImageFilePicker { get; set; }
    protected FilePicker? EditSignatureImageFilePicker { get; set; }
    protected FilePicker? CreateSealImageFilePicker { get; set; }
    protected FilePicker? EditSealImageFilePicker { get; set; }
    protected bool IsUploadingSignatureImage { get; set; }

    protected Dictionary<string, string?> CreateFieldErrors { get; set; } = new();
    protected Dictionary<string, string?> EditFieldErrors { get; set; } = new();

    protected bool HasCreateFieldError(string field) =>
        CreateFieldErrors.ContainsKey(field) && !string.IsNullOrWhiteSpace(CreateFieldErrors[field]);

    protected bool HasEditFieldError(string field) =>
        EditFieldErrors.ContainsKey(field) && !string.IsNullOrWhiteSpace(EditFieldErrors[field]);

    protected string? GetCreateFieldError(string field) => CreateFieldErrors.GetValueOrDefault(field);
    protected string? GetEditFieldError(string field) => EditFieldErrors.GetValueOrDefault(field);

    protected SignType? CreateSignType
    {
        get => Enum.TryParse<SignType>(CreateModel.SignType, out var result) ? result : null;
        set => CreateModel.SignType = value?.ToString() ?? string.Empty;
    }

    protected SignType? EditSignType
    {
        get => Enum.TryParse<SignType>(EditModel.SignType, out var result) ? result : null;
        set => EditModel.SignType = value?.ToString() ?? string.Empty;
    }

    protected override async Task OnParametersSetAsync()
    {
        await LoadSignatureSettingsAsync();
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        if (IdentityUserId == Guid.Empty)
        {
            UserSignatures = new List<UserSignatureWithNavigationPropertiesDto>();
            return;
        }

        var result = await UserSignaturesAppService.GetListAsync(new GetUserSignaturesInput
        {
            IdentityUserId = IdentityUserId,
            MaxResultCount = 100,
            SkipCount = 0,
            Sorting = string.Empty
        });

        UserSignatures = result.Items.ToList();
    }

    private async Task LoadSignatureSettingsAsync(string? signType = null)
    {
        PagedResultDto<LookupDto<Guid>> lookup;
        if (!string.IsNullOrWhiteSpace(signType))
        {
            lookup = await SignatureSettingsAppService.GetSignatureSettingLookupBySignTypeAsync(
                new GetSignatureSettingLookupBySignTypeInput
                {
                    DefaultSignType = signType,
                    MaxResultCount = 1000,
                    SkipCount = 0
                });
        }
        else
        {
            lookup = await SignatureSettingsAppService.GetSignatureSettingLookupAsync(new LookupRequestDto
            {
                MaxResultCount = 1000,
                SkipCount = 0
            });
        }

        SignatureSettingsCollection = lookup.Items.ToList();
        SignatureSettingsIdToCodeMap = SignatureSettingsCollection.ToDictionary(x => x.Id, x => x.DisplayName);
    }

    protected async Task OpenCreateModalAsync()
    {
        CreateModel = new UserSignatureCreateDto
        {
            IdentityUserId = IdentityUserId,
            ValidFrom = DateTime.Now,
            ValidTo = DateTime.Now.AddYears(1),
            IsActive = true
        };
        CreateSignatureSettingId = null;
        CreateFieldErrors.Clear();
        IsUploadingSignatureImage = false;
        await LoadSignatureSettingsAsync();
        await CreateModalRef.Show();
    }

    protected Task CloseCreateModalAsync() => CreateModalRef.Hide();

    protected async Task CreateAsync()
    {
        ClearDigitalOnlyFieldsIfElectronic(CreateModel);
        if (!ValidateCreate())
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            CreateModel.IdentityUserId = IdentityUserId;
            if (CreateSignatureSettingId.HasValue &&
                SignatureSettingsIdToCodeMap.TryGetValue(CreateSignatureSettingId.Value, out var providerCode))
            {
                CreateModel.ProviderCode = providerCode;
            }

            await UserSignaturesAppService.CreateAsync(CreateModel);
            await CloseCreateModalAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message);
        }
    }

    protected async Task OpenEditModalAsync(UserSignatureWithNavigationPropertiesDto item)
    {
        var detail = await UserSignaturesAppService.GetWithNavigationPropertiesAsync(item.UserSignature.Id);
        EditingId = detail.UserSignature.Id;
        EditModel = ObjectMapper.Map<UserSignatureDto, UserSignatureUpdateDto>(detail.UserSignature);
        EditSignatureSettingId = SignatureSettingsCollection
            .Where(x => x.DisplayName == EditModel.ProviderCode)
            .Select(x => (Guid?)x.Id)
            .FirstOrDefault();
        EditFieldErrors.Clear();
        IsUploadingSignatureImage = false;
        await LoadSignatureSettingsAsync(EditModel.SignType);
        await EditModalRef.Show();
    }

    protected Task CloseEditModalAsync() => EditModalRef.Hide();

    protected async Task UpdateAsync()
    {
        ClearDigitalOnlyFieldsIfElectronic(EditModel);
        if (!ValidateEdit())
        {
            await InvokeAsync(StateHasChanged);
            return;
        }

        try
        {
            if (EditSignatureSettingId.HasValue &&
                SignatureSettingsIdToCodeMap.TryGetValue(EditSignatureSettingId.Value, out var providerCode))
            {
                EditModel.ProviderCode = providerCode;
            }

            EditModel.IdentityUserId = IdentityUserId;
            await UserSignaturesAppService.UpdateAsync(EditingId, EditModel);
            await CloseEditModalAsync();
            await LoadAsync();
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message);
        }
    }

    protected async Task DeleteAsync(UserSignatureWithNavigationPropertiesDto item)
    {
        if (await UiMessageService.Confirm(L["DeleteConfirmationMessage"].Value))
        {
            await UserSignaturesAppService.DeleteAsync(item.UserSignature.Id);
            await LoadAsync();
        }
    }

    protected async Task OnCreateSignTypeChangedAsync(SignType? value)
    {
        CreateSignType = value;
        CreateFieldErrors.Remove("SignType");
        CreateSignatureSettingId = null;
        if (!IsDigitalSignType(value?.ToString()))
        {
            CreateModel.TokenRef = null;
            CreateModel.Secret = null;
            CreateModel.SealImg = null;
        }

        await LoadSignatureSettingsAsync(value?.ToString());
        await InvokeAsync(StateHasChanged);
    }

    protected async Task OnEditSignTypeChangedAsync(SignType? value)
    {
        EditSignType = value;
        EditFieldErrors.Remove("SignType");
        EditSignatureSettingId = null;
        if (!IsDigitalSignType(value?.ToString()))
        {
            EditModel.TokenRef = null;
            EditModel.Secret = null;
            EditModel.SealImg = null;
        }

        await LoadSignatureSettingsAsync(value?.ToString());
        await InvokeAsync(StateHasChanged);
    }

    protected async Task UploadSignatureImageFileAsync(FileChangedEventArgs e, bool isEditMode)
    {
        await UploadImageFileAsync(e, isEditMode, "user-signature-images", isSeal: false);
    }

    protected async Task UploadSealImageFileAsync(FileChangedEventArgs e, bool isEditMode)
    {
        await UploadImageFileAsync(e, isEditMode, "user-seal-images", isSeal: true);
    }

    private async Task UploadImageFileAsync(FileChangedEventArgs e, bool isEditMode, string folder, bool isSeal)
    {
        try
        {
            var file = e.Files.FirstOrDefault();
            if (file == null)
            {
                if (isEditMode)
                {
                    if (isSeal)
                    {
                        EditModel.SealImg = null;
                    }
                    else
                    {
                        EditModel.SignatureImage = string.Empty;
                    }
                }
                else
                {
                    if (isSeal)
                    {
                        CreateModel.SealImg = null;
                    }
                    else
                    {
                        CreateModel.SignatureImage = string.Empty;
                    }
                }

                await InvokeAsync(StateHasChanged);
                return;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.Name).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                await UiMessageService.Error(L["OnlyImageFilesAllowed"]);
                await ClearFilePickerAsync(isEditMode, isSeal);
                return;
            }

            if (file.Size > 52428800)
            {
                await UiMessageService.Error(L["FileSizeTooLarge"]);
                await ClearFilePickerAsync(isEditMode, isSeal);
                return;
            }

            IsUploadingSignatureImage = true;
            await InvokeAsync(StateHasChanged);

            using var memoryStream = new MemoryStream();
            await file.OpenReadStream(long.MaxValue).CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            var safeFileName = BlobStoragePathHelper.SanitizeFileName(file.Name);
            var filePath = $"{folder}/{Guid.NewGuid()}_{safeFileName}";
            await BlobContainer.SaveAsync(filePath, memoryStream.ToArray(), overrideExisting: true);

            if (isEditMode)
            {
                if (isSeal)
                {
                    EditModel.SealImg = filePath;
                    EditFieldErrors.Remove("SealImg");
                }
                else
                {
                    EditModel.SignatureImage = filePath;
                    EditFieldErrors.Remove("SignatureImage");
                }
            }
            else
            {
                if (isSeal)
                {
                    CreateModel.SealImg = filePath;
                    CreateFieldErrors.Remove("SealImg");
                }
                else
                {
                    CreateModel.SignatureImage = filePath;
                    CreateFieldErrors.Remove("SignatureImage");
                }
            }

            await UiMessageService.Success(L["FileUploadedSuccessfully"]);
        }
        catch (Exception ex)
        {
            await UiMessageService.Error(ex.Message);
        }
        finally
        {
            IsUploadingSignatureImage = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    private async Task ClearFilePickerAsync(bool isEditMode, bool isSeal)
    {
        if (isEditMode)
        {
            if (isSeal && EditSealImageFilePicker != null)
            {
                await EditSealImageFilePicker.Clear();
            }
            else if (!isSeal && EditSignatureImageFilePicker != null)
            {
                await EditSignatureImageFilePicker.Clear();
            }
        }
        else
        {
            if (isSeal && CreateSealImageFilePicker != null)
            {
                await CreateSealImageFilePicker.Clear();
            }
            else if (!isSeal && CreateSignatureImageFilePicker != null)
            {
                await CreateSignatureImageFilePicker.Clear();
            }
        }
    }

    protected string GetSignatureImageUrl(string? imagePath)
    {
        if (string.IsNullOrEmpty(imagePath))
        {
            return string.Empty;
        }

        return BlobDisplayUrlProvider.GetDisplayUrl(imagePath);
    }

    protected string GetSignTypeDisplayName(string? signType)
    {
        if (string.IsNullOrWhiteSpace(signType) || !Enum.TryParse<SignType>(signType, out var parsed))
        {
            return signType ?? string.Empty;
        }

        return L[$"Enum:SignType.{parsed}"];
    }

    protected static bool IsDigitalSignType(string? signType)
    {
        return string.Equals(signType, nameof(SignType.DIGITAL), StringComparison.OrdinalIgnoreCase);
    }

    private static void ClearDigitalOnlyFieldsIfElectronic(UserSignatureCreateDto dto)
    {
        if (!IsDigitalSignType(dto.SignType))
        {
            dto.TokenRef = null;
            dto.Secret = null;
            dto.SealImg = null;
        }
    }

    private static void ClearDigitalOnlyFieldsIfElectronic(UserSignatureUpdateDto dto)
    {
        if (!IsDigitalSignType(dto.SignType))
        {
            dto.TokenRef = null;
            dto.Secret = null;
            dto.SealImg = null;
        }
    }

    private bool ValidateCreate()
    {
        CreateFieldErrors.Clear();
        if (string.IsNullOrWhiteSpace(CreateModel.SignType))
        {
            CreateFieldErrors["SignType"] = L["TheFieldIsRequired", L["SignType"]];
        }

        if (!CreateSignatureSettingId.HasValue || CreateSignatureSettingId.Value == Guid.Empty)
        {
            CreateFieldErrors["ProviderCode"] = L["TheFieldIsRequired", L["ProviderCode"]];
        }

        if (string.IsNullOrWhiteSpace(CreateModel.SignatureImage))
        {
            CreateFieldErrors["SignatureImage"] = L["TheFieldIsRequired", L["SignatureImage"]];
        }

        if (IsDigitalSignType(CreateModel.SignType))
        {
            if (string.IsNullOrWhiteSpace(CreateModel.TokenRef))
            {
                CreateFieldErrors["TokenRef"] = L["TokenRefRequiredForDigitalSign"];
            }

            if (string.IsNullOrWhiteSpace(CreateModel.Secret))
            {
                CreateFieldErrors["Secret"] = L["SecretRequiredForDigitalSign"];
            }

            if (string.IsNullOrWhiteSpace(CreateModel.SealImg))
            {
                CreateFieldErrors["SealImg"] = L["SealImgRequiredForDigitalSign"];
            }
        }

        return CreateFieldErrors.Count == 0;
    }

    private bool ValidateEdit()
    {
        EditFieldErrors.Clear();
        if (string.IsNullOrWhiteSpace(EditModel.SignType))
        {
            EditFieldErrors["SignType"] = L["TheFieldIsRequired", L["SignType"]];
        }

        if (!EditSignatureSettingId.HasValue || EditSignatureSettingId.Value == Guid.Empty)
        {
            EditFieldErrors["ProviderCode"] = L["TheFieldIsRequired", L["ProviderCode"]];
        }

        if (string.IsNullOrWhiteSpace(EditModel.SignatureImage))
        {
            EditFieldErrors["SignatureImage"] = L["TheFieldIsRequired", L["SignatureImage"]];
        }

        if (IsDigitalSignType(EditModel.SignType))
        {
            if (string.IsNullOrWhiteSpace(EditModel.TokenRef))
            {
                EditFieldErrors["TokenRef"] = L["TokenRefRequiredForDigitalSign"];
            }

            if (string.IsNullOrWhiteSpace(EditModel.SealImg))
            {
                EditFieldErrors["SealImg"] = L["SealImgRequiredForDigitalSign"];
            }
        }

        return EditFieldErrors.Count == 0;
    }
}
