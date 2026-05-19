using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;
using Volo.Abp.Data;
using HC.BlobStoring;

namespace HC.UserSignatures;

public abstract class UserSignatureManagerBase : DomainService
{
    protected IUserSignatureRepository _userSignatureRepository;

    public UserSignatureManagerBase(IUserSignatureRepository userSignatureRepository)
    {
        _userSignatureRepository = userSignatureRepository;
    }

    public virtual async Task<UserSignature> CreateAsync(
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
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNullOrWhiteSpace(signType, nameof(signType));
        Check.NotNullOrWhiteSpace(providerCode, nameof(providerCode));
        Check.NotNullOrWhiteSpace(signatureImage, nameof(signatureImage));
        ValidateBlobImagePath(signatureImage, nameof(signatureImage));
        if (!string.IsNullOrWhiteSpace(sealImg))
        {
            ValidateBlobImagePath(sealImg, nameof(sealImg));
        }

        var userSignature = new UserSignature(GuidGenerator.Create(), identityUserId, signType, providerCode, signatureImage, isActive, tokenRef, secret, sealImg, validFrom, validTo);
        return await _userSignatureRepository.InsertAsync(userSignature);
    }

    public virtual async Task<UserSignature> UpdateAsync(
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
        DateTime? validTo = null,
        [CanBeNull] string? concurrencyStamp = null)
    {
        Check.NotNull(identityUserId, nameof(identityUserId));
        Check.NotNullOrWhiteSpace(signType, nameof(signType));
        Check.NotNullOrWhiteSpace(providerCode, nameof(providerCode));
        Check.NotNullOrWhiteSpace(signatureImage, nameof(signatureImage));
        ValidateBlobImagePath(signatureImage, nameof(signatureImage));
        if (!string.IsNullOrWhiteSpace(sealImg))
        {
            ValidateBlobImagePath(sealImg, nameof(sealImg));
        }

        var userSignature = await _userSignatureRepository.GetAsync(id);
        userSignature.IdentityUserId = identityUserId;
        userSignature.SignType = signType;
        userSignature.ProviderCode = providerCode;
        userSignature.SignatureImage = signatureImage;
        userSignature.IsActive = isActive;
        userSignature.TokenRef = tokenRef;
        if (!string.IsNullOrWhiteSpace(secret))
        {
            userSignature.Secret = secret;
        }
        userSignature.SealImg = sealImg;
        userSignature.ValidFrom = validFrom;
        userSignature.ValidTo = validTo;
        userSignature.SetConcurrencyStampIfNotNull(concurrencyStamp);
        return await _userSignatureRepository.UpdateAsync(userSignature);
    }

    private static void ValidateBlobImagePath(string path, string parameterName)
    {
        if (!BlobStoragePathHelper.IsBlobStoragePath(path))
        {
            throw new BusinessException("HC:InvalidBlobImagePath")
                .WithData("ParameterName", parameterName);
        }
    }
}