using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.UserSignatures;

public abstract class UserSignatureBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string SignType { get; set; }

    [NotNull]
    public virtual string ProviderCode { get; set; }

    [CanBeNull]
    public virtual string? TokenRef { get; set; }

    [CanBeNull]
    public virtual string? Secret { get; set; }

    [CanBeNull]
    public virtual string? SealImg { get; set; }

    [NotNull]
    public virtual string SignatureImage { get; set; }

    public virtual DateTime? ValidFrom { get; set; }

    public virtual DateTime? ValidTo { get; set; }

    public virtual bool IsActive { get; set; }

    public Guid IdentityUserId { get; set; }

    protected UserSignatureBase()
    {
    }

    public UserSignatureBase(
        Guid id,
        Guid identityUserId,
        string signType,
        string providerCode,
        string signatureImage,
        bool isActive,
        string? tokenRef = null,
        string? secret = null,
        string? sealImg = null,
        DateTime? validFrom = null,
        DateTime? validTo = null)
    {
        Id = id;
        Check.NotNull(signType, nameof(signType));
        Check.NotNull(providerCode, nameof(providerCode));
        Check.NotNull(signatureImage, nameof(signatureImage));
        SignType = signType;
        ProviderCode = providerCode;
        SignatureImage = signatureImage;
        IsActive = isActive;
        TokenRef = tokenRef;
        Secret = secret;
        SealImg = sealImg;
        ValidFrom = validFrom;
        ValidTo = validTo;
        IdentityUserId = identityUserId;
    }
}