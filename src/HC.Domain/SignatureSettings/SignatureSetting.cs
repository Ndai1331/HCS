using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.SignatureSettings;

public abstract class SignatureSettingBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string ProviderCode { get; set; }

    [NotNull]
    public virtual string ProviderType { get; set; }

    [NotNull]
    public virtual string ApiEndpoint { get; set; }

    [CanBeNull]
    public virtual string? LayoutImg { get; set; }

    public virtual int ApiTimeout { get; set; }

    [NotNull]
    public virtual string DefaultSignType { get; set; }

    public virtual bool AllowElectronicSign { get; set; }

    public virtual bool AllowDigitalSign { get; set; }

    public virtual bool RequireOtp { get; set; }

    public virtual int SignWidth { get; set; }

    public virtual int SignHeight { get; set; }

    [NotNull]
    public virtual string SignedFileSuffix { get; set; }

    public virtual bool KeepOriginalFile { get; set; }

    public virtual bool OverwriteSignedFile { get; set; }

    public virtual bool EnableSignLog { get; set; }

    public virtual bool IsActive { get; set; }

    protected SignatureSettingBase()
    {
    }

    public SignatureSettingBase(Guid id, string providerCode, string providerType, string apiEndpoint, int apiTimeout, string defaultSignType, bool allowElectronicSign, bool allowDigitalSign, bool requireOtp, int signWidth, int signHeight, string signedFileSuffix, bool keepOriginalFile, bool overwriteSignedFile, bool enableSignLog, bool isActive, string? layoutImg = null)
    {
        Id = id;
        Check.NotNull(providerCode, nameof(providerCode));
        Check.NotNull(providerType, nameof(providerType));
        Check.NotNull(apiEndpoint, nameof(apiEndpoint));
        Check.NotNull(defaultSignType, nameof(defaultSignType));
        Check.NotNull(signedFileSuffix, nameof(signedFileSuffix));
        ProviderCode = providerCode;
        ProviderType = providerType;
        ApiEndpoint = apiEndpoint;
        LayoutImg = layoutImg;
        ApiTimeout = apiTimeout;
        DefaultSignType = defaultSignType;
        AllowElectronicSign = allowElectronicSign;
        AllowDigitalSign = allowDigitalSign;
        RequireOtp = requireOtp;
        SignWidth = signWidth;
        SignHeight = signHeight;
        SignedFileSuffix = signedFileSuffix;
        KeepOriginalFile = keepOriginalFile;
        OverwriteSignedFile = overwriteSignedFile;
        EnableSignLog = enableSignLog;
        IsActive = isActive;
    }
}