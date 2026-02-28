using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;

namespace HC.SignatureSettings;

public abstract class SignatureSettingManagerBase : DomainService
{
    protected ISignatureSettingRepository _signatureSettingRepository;

    public SignatureSettingManagerBase(ISignatureSettingRepository signatureSettingRepository)
    {
        _signatureSettingRepository = signatureSettingRepository;
    }

    public virtual async Task<SignatureSetting> CreateAsync(string providerCode, string providerType, string apiEndpoint, int apiTimeout, string defaultSignType, bool allowElectronicSign, bool allowDigitalSign, bool requireOtp, int signWidth, int signHeight, string signedFileSuffix, bool keepOriginalFile, bool overwriteSignedFile, bool enableSignLog, bool isActive, string? layoutImg = null)
    {
        Check.NotNullOrWhiteSpace(providerCode, nameof(providerCode));
        Check.NotNullOrWhiteSpace(providerType, nameof(providerType));
        Check.NotNullOrWhiteSpace(apiEndpoint, nameof(apiEndpoint));
        Check.NotNullOrWhiteSpace(defaultSignType, nameof(defaultSignType));
        Check.NotNullOrWhiteSpace(signedFileSuffix, nameof(signedFileSuffix));
        var signatureSetting = new SignatureSetting(GuidGenerator.Create(), providerCode, providerType, apiEndpoint, apiTimeout, defaultSignType, allowElectronicSign, allowDigitalSign, requireOtp, signWidth, signHeight, signedFileSuffix, keepOriginalFile, overwriteSignedFile, enableSignLog, isActive, layoutImg);
        return await _signatureSettingRepository.InsertAsync(signatureSetting);
    }

    public virtual async Task<SignatureSetting> UpdateAsync(Guid id, string providerCode, string providerType, string apiEndpoint, int apiTimeout, string defaultSignType, bool allowElectronicSign, bool allowDigitalSign, bool requireOtp, int signWidth, int signHeight, string signedFileSuffix, bool keepOriginalFile, bool overwriteSignedFile, bool enableSignLog, bool isActive, string? layoutImg = null, [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNullOrWhiteSpace(providerCode, nameof(providerCode));
        Check.NotNullOrWhiteSpace(providerType, nameof(providerType));
        Check.NotNullOrWhiteSpace(apiEndpoint, nameof(apiEndpoint));
        Check.NotNullOrWhiteSpace(defaultSignType, nameof(defaultSignType));
        Check.NotNullOrWhiteSpace(signedFileSuffix, nameof(signedFileSuffix));
        var signatureSetting = await _signatureSettingRepository.GetAsync(id);
        signatureSetting.ProviderCode = providerCode;
        signatureSetting.ProviderType = providerType;
        signatureSetting.ApiEndpoint = apiEndpoint;
        signatureSetting.LayoutImg = layoutImg;
        signatureSetting.ApiTimeout = apiTimeout;
        signatureSetting.DefaultSignType = defaultSignType;
        signatureSetting.AllowElectronicSign = allowElectronicSign;
        signatureSetting.AllowDigitalSign = allowDigitalSign;
        signatureSetting.RequireOtp = requireOtp;
        signatureSetting.SignWidth = signWidth;
        signatureSetting.SignHeight = signHeight;
        signatureSetting.SignedFileSuffix = signedFileSuffix;
        signatureSetting.KeepOriginalFile = keepOriginalFile;
        signatureSetting.OverwriteSignedFile = overwriteSignedFile;
        signatureSetting.EnableSignLog = enableSignLog;
        signatureSetting.IsActive = isActive;
        signatureSetting.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _signatureSettingRepository.UpdateAsync(signatureSetting);
    }
}