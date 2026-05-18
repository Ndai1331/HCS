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
using HC.SignatureSettings;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;
using System.Globalization;
using HC.Documents;

namespace HC.SignatureSettings;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.SignatureSettingsDefault)]
public abstract class SignatureSettingsAppServiceBase : HCAppService
{
    protected IDistributedCache<SignatureSettingDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISignatureSettingRepository _signatureSettingRepository;
    protected SignatureSettingManager _signatureSettingManager;
    protected IDistributedCache<DocumentsLookupCacheItem, string> _lookupCache
        => LazyServiceProvider.LazyGetRequiredService<IDistributedCache<DocumentsLookupCacheItem, string>>();
    protected IDistributedCache<LookupCacheVersionCacheItem, string> _lookupVersionCache
        => LazyServiceProvider.LazyGetRequiredService<IDistributedCache<LookupCacheVersionCacheItem, string>>();

    public SignatureSettingsAppServiceBase(ISignatureSettingRepository signatureSettingRepository, SignatureSettingManager signatureSettingManager, IDistributedCache<SignatureSettingDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _signatureSettingRepository = signatureSettingRepository;
        _signatureSettingManager = signatureSettingManager;
    }

    public virtual async Task<PagedResultDto<SignatureSettingDto>> GetListAsync(GetSignatureSettingsInput input)
    {
        var totalCount = await _signatureSettingRepository.GetCountAsync(input.FilterText, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeoutMin, input.ApiTimeoutMax, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidthMin, input.SignWidthMax, input.SignHeightMin, input.SignHeightMax, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive);
        var items = await _signatureSettingRepository.GetListAsync(input.FilterText, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeoutMin, input.ApiTimeoutMax, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidthMin, input.SignWidthMax, input.SignHeightMin, input.SignHeightMax, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SignatureSettingDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SignatureSetting>, List<SignatureSettingDto>>(items)
        };
    }

    public virtual async Task<SignatureSettingDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SignatureSetting, SignatureSettingDto>(await _signatureSettingRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsDelete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _signatureSettingRepository.DeleteAsync(id);
        await InvalidateLookupCacheAsync();
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsCreate)]
    public virtual async Task<SignatureSettingDto> CreateAsync(SignatureSettingCreateDto input)
    {
        var signatureSetting = await _signatureSettingManager.CreateAsync(input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeout, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidth, input.SignHeight, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive, input.LayoutImg);
        await InvalidateLookupCacheAsync();
        return ObjectMapper.Map<SignatureSetting, SignatureSettingDto>(signatureSetting);
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsEdit)]
    public virtual async Task<SignatureSettingDto> UpdateAsync(Guid id, SignatureSettingUpdateDto input)
    {
        var signatureSetting = await _signatureSettingManager.UpdateAsync(id, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeout, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidth, input.SignHeight, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive, input.LayoutImg, input.ConcurrencyStamp);
        await InvalidateLookupCacheAsync();
        return ObjectMapper.Map<SignatureSetting, SignatureSettingDto>(signatureSetting);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SignatureSettingExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _signatureSettingRepository.GetListAsync(input.FilterText, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeoutMin, input.ApiTimeoutMax, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidthMin, input.SignWidthMax, input.SignHeightMin, input.SignHeightMax, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<SignatureSetting>, List<SignatureSettingExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SignatureSettings.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsDelete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> signaturesettingIds)
    {
        await _signatureSettingRepository.DeleteManyAsync(signaturesettingIds);
        await InvalidateLookupCacheAsync();
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsDelete)]
    public virtual async Task DeleteAllAsync(GetSignatureSettingsInput input)
    {
        await _signatureSettingRepository.DeleteAllAsync(input.FilterText, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeoutMin, input.ApiTimeoutMax, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidthMin, input.SignWidthMax, input.SignHeightMin, input.SignHeightMax, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive);
        await InvalidateLookupCacheAsync();
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SignatureSettingDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupAsync(LookupRequestDto input)
    {
        // Cache key schema: signature:lookup:default:tenant:{tenantId|host}:lang:{language}:filter:{filter}:skip:{skip}:take:{take}
        var cacheKey = await BuildLookupCacheKeyAsync("default", input.Filter, null, input.SkipCount, input.MaxResultCount);
        var cached = await _lookupCache.GetOrAddAsync(cacheKey, () => LoadSignatureLookupAsync(input.Filter, null, input.SkipCount, input.MaxResultCount),
            () => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
        return new PagedResultDto<LookupDto<Guid>> { TotalCount = cached!.TotalCount, Items = cached.Items };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupBySignTypeAsync(GetSignatureSettingLookupBySignTypeInput input)
    {
        // Cache key schema: signature:lookup:by-sign-type:tenant:{tenantId|host}:lang:{language}:filter:{filter}:signType:{signType}:skip:{skip}:take:{take}
        var cacheKey = await BuildLookupCacheKeyAsync("by-sign-type", input.Filter, input.DefaultSignType, input.SkipCount, input.MaxResultCount);
        var cached = await _lookupCache.GetOrAddAsync(cacheKey, () => LoadSignatureLookupAsync(input.Filter, input.DefaultSignType, input.SkipCount, input.MaxResultCount),
            () => new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10) });
        return new PagedResultDto<LookupDto<Guid>> { TotalCount = cached!.TotalCount, Items = cached.Items };
    }

    protected async Task<DocumentsLookupCacheItem> LoadSignatureLookupAsync(string? filter, string? defaultSignType, int skipCount, int maxResultCount)
    {
        var query = (await _signatureSettingRepository.GetQueryableAsync()).Where(x => x.IsActive);
        if (!string.IsNullOrWhiteSpace(filter)) query = query.Where(x => x.ProviderCode != null && x.ProviderCode.Contains(filter));
        if (!string.IsNullOrWhiteSpace(defaultSignType)) query = query.Where(x => x.DefaultSignType == defaultSignType);
        var totalCount = await AsyncExecuter.LongCountAsync(query);
        var items = await AsyncExecuter.ToListAsync(query.OrderBy(x => x.ProviderCode)
            .Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.ProviderCode })
            .PageBy(skipCount, maxResultCount));
        return new DocumentsLookupCacheItem { TotalCount = totalCount, Items = items };
    }

    protected async Task<string> BuildLookupCacheKeyAsync(string scope, string? filter, string? signType, int skipCount, int maxResultCount)
    {
        var tenant = CurrentTenant.Id?.ToString("N") ?? "host";
        var language = CultureInfo.CurrentUICulture.Name?.ToLowerInvariant() ?? "iv";
        var version = (await _lookupVersionCache.GetAsync("lookup-version:signature-provider"))?.Version ?? 1;
        return $"signature:lookup:{scope}:v{version}:tenant:{tenant}:lang:{language}:filter:{(filter ?? string.Empty).Trim().ToLowerInvariant()}:signType:{(signType ?? string.Empty).Trim().ToLowerInvariant()}:skip:{skipCount}:take:{maxResultCount}";
    }

    protected virtual async Task InvalidateLookupCacheAsync()
    {
        var current = await _lookupVersionCache.GetAsync("lookup-version:signature-provider");
        await _lookupVersionCache.SetAsync(
            "lookup-version:signature-provider",
            new LookupCacheVersionCacheItem { Version = (current?.Version ?? 1) + 1 },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
    }
}
