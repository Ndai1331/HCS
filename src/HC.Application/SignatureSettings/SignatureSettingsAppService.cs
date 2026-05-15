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

namespace HC.SignatureSettings;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.SignatureSettingsDefault)]
public abstract class SignatureSettingsAppServiceBase : HCAppService
{
    protected IDistributedCache<SignatureSettingDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISignatureSettingRepository _signatureSettingRepository;
    protected SignatureSettingManager _signatureSettingManager;

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
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsCreate)]
    public virtual async Task<SignatureSettingDto> CreateAsync(SignatureSettingCreateDto input)
    {
        var signatureSetting = await _signatureSettingManager.CreateAsync(input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeout, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidth, input.SignHeight, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive, input.LayoutImg);
        return ObjectMapper.Map<SignatureSetting, SignatureSettingDto>(signatureSetting);
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsEdit)]
    public virtual async Task<SignatureSettingDto> UpdateAsync(Guid id, SignatureSettingUpdateDto input)
    {
        var signatureSetting = await _signatureSettingManager.UpdateAsync(id, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeout, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidth, input.SignHeight, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive, input.LayoutImg, input.ConcurrencyStamp);
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
    }

    [Authorize(HCPermissions.MasterDatas.SignatureSettingsDelete)]
    public virtual async Task DeleteAllAsync(GetSignatureSettingsInput input)
    {
        await _signatureSettingRepository.DeleteAllAsync(input.FilterText, input.ProviderCode, input.ProviderType, input.ApiEndpoint, input.ApiTimeoutMin, input.ApiTimeoutMax, input.DefaultSignType, input.AllowElectronicSign, input.AllowDigitalSign, input.RequireOtp, input.SignWidthMin, input.SignWidthMax, input.SignHeightMin, input.SignHeightMax, input.SignedFileSuffix, input.KeepOriginalFile, input.OverwriteSignedFile, input.EnableSignLog, input.IsActive);
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
        var query = await _signatureSettingRepository.GetQueryableAsync();
        
        // Filter by ProviderCode if filter is provided
        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(x => x.ProviderCode != null && x.ProviderCode.Contains(input.Filter));
        }
        
        // Only get active signature settings
        query = query.Where(x => x.IsActive);
        
        var lookupData = await AsyncExecuter.ToListAsync(
            query
                .Select(x => new { x.Id, x.ProviderCode })
                .OrderBy(x => x.ProviderCode)
                .PageBy(input.SkipCount, input.MaxResultCount)
        );
        
        var totalCount = await AsyncExecuter.CountAsync(query);
        
        var items = lookupData.Select(x => new LookupDto<Guid>
        {
            Id = x.Id,
            DisplayName = x.ProviderCode
        }).ToList();
        
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = items
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupBySignTypeAsync(GetSignatureSettingLookupBySignTypeInput input)
    {
        var query = await _signatureSettingRepository.GetQueryableAsync();

        if (!string.IsNullOrWhiteSpace(input.Filter))
        {
            query = query.Where(x => x.ProviderCode != null && x.ProviderCode.Contains(input.Filter));
        }

        query = query.Where(x => x.IsActive);

        if (!string.IsNullOrWhiteSpace(input.DefaultSignType))
        {
            query = query.Where(x => x.DefaultSignType == input.DefaultSignType);
        }

        var lookupData = await AsyncExecuter.ToListAsync(
            query
                .Select(x => new { x.Id, x.ProviderCode })
                .OrderBy(x => x.ProviderCode)
                .PageBy(input.SkipCount, input.MaxResultCount)
        );

        var totalCount = await AsyncExecuter.CountAsync(query);

        var items = lookupData.Select(x => new LookupDto<Guid>
        {
            Id = x.Id,
            DisplayName = x.ProviderCode
        }).ToList();

        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = items
        };
    }
}