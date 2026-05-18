using HC.Shared;
using Volo.Abp.Identity;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.UserSignatures;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.UserSignatures;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.UserSignatures.Default)]
public abstract class UserSignaturesAppServiceBase : HCAppService
{
    protected IDistributedCache<UserSignatureDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IUserSignatureRepository _userSignatureRepository;
    protected UserSignatureManager _userSignatureManager;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public UserSignaturesAppServiceBase(IUserSignatureRepository userSignatureRepository, UserSignatureManager userSignatureManager, IDistributedCache<UserSignatureDownloadTokenCacheItem, string> downloadTokenCache, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _userSignatureRepository = userSignatureRepository;
        _userSignatureManager = userSignatureManager;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<UserSignatureWithNavigationPropertiesDto>> GetListAsync(GetUserSignaturesInput input)
    {
        var totalCount = await _userSignatureRepository.GetCountAsync(input.FilterText, input.SignType, input.ProviderCode, input.TokenRef, input.SignatureImage, input.ValidFromMin, input.ValidFromMax, input.ValidToMin, input.ValidToMax, input.IsActive, input.IdentityUserId);
        var items = await _userSignatureRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.SignType, input.ProviderCode, input.TokenRef, input.SignatureImage, input.ValidFromMin, input.ValidFromMax, input.ValidToMin, input.ValidToMax, input.IsActive, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<UserSignatureWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<UserSignatureWithNavigationProperties>, List<UserSignatureWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<UserSignatureWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<UserSignatureWithNavigationProperties, UserSignatureWithNavigationPropertiesDto>(await _userSignatureRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<UserSignatureDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<UserSignature, UserSignatureDto>(await _userSignatureRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.UserName != null && x.UserName.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.UserSignatures.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _userSignatureRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.UserSignatures.Create)]
    public virtual async Task<UserSignatureDto> CreateAsync(UserSignatureCreateDto input)
    {
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var userSignature = await _userSignatureManager.CreateAsync(
            input.IdentityUserId,
            input.SignType,
            input.ProviderCode,
            input.SignatureImage,
            input.IsActive,
            input.TokenRef,
            input.Secret,
            input.SealImg,
            input.ValidFrom,
            input.ValidTo);
        return ObjectMapper.Map<UserSignature, UserSignatureDto>(userSignature);
    }

    [Authorize(HCPermissions.UserSignatures.Edit)]
    public virtual async Task<UserSignatureDto> UpdateAsync(Guid id, UserSignatureUpdateDto input)
    {
        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var userSignature = await _userSignatureManager.UpdateAsync(
            id,
            input.IdentityUserId,
            input.SignType,
            input.ProviderCode,
            input.SignatureImage,
            input.IsActive,
            input.TokenRef,
            input.Secret,
            input.SealImg,
            input.ValidFrom,
            input.ValidTo,
            input.ConcurrencyStamp);
        return ObjectMapper.Map<UserSignature, UserSignatureDto>(userSignature);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserSignatureExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var userSignatures = await _userSignatureRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.SignType, input.ProviderCode, input.TokenRef, input.SignatureImage, input.ValidFromMin, input.ValidFromMax, input.ValidToMin, input.ValidToMax, input.IsActive, input.IdentityUserId);
        var items = userSignatures.Select(item => new
        {
            SignType = item.UserSignature.SignType,
            ProviderCode = item.UserSignature.ProviderCode,
            TokenRef = item.UserSignature.TokenRef,
            SealImg = item.UserSignature.SealImg,
            SignatureImage = item.UserSignature.SignatureImage,
            ValidFrom = item.UserSignature.ValidFrom,
            ValidTo = item.UserSignature.ValidTo,
            IsActive = item.UserSignature.IsActive,
            IdentityUser = item.IdentityUser?.Name,
        });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "UserSignatures.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.UserSignatures.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> usersignatureIds)
    {
        await _userSignatureRepository.DeleteManyAsync(usersignatureIds);
    }

    [Authorize(HCPermissions.UserSignatures.Delete)]
    public virtual async Task DeleteAllAsync(GetUserSignaturesInput input)
    {
        await _userSignatureRepository.DeleteAllAsync(input.FilterText, input.SignType, input.ProviderCode, input.TokenRef, input.SignatureImage, input.ValidFromMin, input.ValidFromMax, input.ValidToMin, input.ValidToMax, input.IsActive, input.IdentityUserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new UserSignatureDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}
