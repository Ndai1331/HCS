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
using HC.MasterDatas;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.MasterDatas;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.Default)]
public abstract class MasterDatasAppServiceBase : HCAppService
{
    protected IDistributedCache<MasterDataDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDistributedCache<LookupCacheVersionCacheItem, string> _lookupVersionCache;
    protected IMasterDataRepository _masterDataRepository;
    protected MasterDataManager _masterDataManager;

    public MasterDatasAppServiceBase(IMasterDataRepository masterDataRepository, MasterDataManager masterDataManager, IDistributedCache<MasterDataDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _masterDataRepository = masterDataRepository;
        _masterDataManager = masterDataManager;
        _lookupVersionCache = LazyServiceProvider.LazyGetRequiredService<IDistributedCache<LookupCacheVersionCacheItem, string>>();
    }

    public virtual async Task<PagedResultDto<MasterDataDto>> GetListAsync(GetMasterDatasInput input)
    {
        var totalCount = await _masterDataRepository.GetCountAsync(input.FilterText, input.Type, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
        var items = await _masterDataRepository.GetListAsync(input.FilterText, input.Type, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<MasterDataDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<MasterData>, List<MasterDataDto>>(items)
        };
    }

    public virtual async Task<MasterDataDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<MasterData, MasterDataDto>(await _masterDataRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _masterDataRepository.DeleteAsync(id);
        await InvalidateLookupCacheAsync();
    }

    [Authorize(HCPermissions.MasterDatas.Create)]
    public virtual async Task<MasterDataDto> CreateAsync(MasterDataCreateDto input)
    {
        var masterData = await _masterDataManager.CreateAsync(input.Type, input.Code, input.Name, input.SortOrder, input.IsActive);
        await InvalidateLookupCacheAsync();
        return ObjectMapper.Map<MasterData, MasterDataDto>(masterData);
    }

    [Authorize(HCPermissions.MasterDatas.Edit)]
    public virtual async Task<MasterDataDto> UpdateAsync(Guid id, MasterDataUpdateDto input)
    {
        var masterData = await _masterDataManager.UpdateAsync(id, input.Type, input.Code, input.Name, input.SortOrder, input.IsActive, input.ConcurrencyStamp);
        await InvalidateLookupCacheAsync();
        return ObjectMapper.Map<MasterData, MasterDataDto>(masterData);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(MasterDataExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _masterDataRepository.GetListAsync(input.FilterText, input.Type, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<MasterData>, List<MasterDataExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "MasterDatas.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> masterdataIds)
    {
        await _masterDataRepository.DeleteManyAsync(masterdataIds);
        await InvalidateLookupCacheAsync();
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteAllAsync(GetMasterDatasInput input)
    {
        await _masterDataRepository.DeleteAllAsync(input.FilterText, input.Type, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
        await InvalidateLookupCacheAsync();
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new MasterDataDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }

    protected virtual async Task InvalidateLookupCacheAsync()
    {
        var current = await _lookupVersionCache.GetAsync("lookup-version:master-data");
        await _lookupVersionCache.SetAsync(
            "lookup-version:master-data",
            new LookupCacheVersionCacheItem { Version = (current?.Version ?? 1) + 1 },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
    }
}
