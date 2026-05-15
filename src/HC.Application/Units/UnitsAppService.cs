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
using HC.Units;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.Units;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.UnitDefault)]
public abstract class UnitsAppServiceBase : HCAppService
{
    protected IDistributedCache<UnitDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IUnitRepository _unitRepository;
    protected UnitManager _unitManager;

    public UnitsAppServiceBase(IUnitRepository unitRepository, UnitManager unitManager, IDistributedCache<UnitDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _unitRepository = unitRepository;
        _unitManager = unitManager;
    }

    public virtual async Task<PagedResultDto<UnitDto>> GetListAsync(GetUnitsInput input)
    {
        var totalCount = await _unitRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
        var items = await _unitRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<UnitDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Unit>, List<UnitDto>>(items)
        };
    }

    public virtual async Task<UnitDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Unit, UnitDto>(await _unitRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.MasterDatas.UnitDelete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _unitRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.MasterDatas.UnitCreate)]
    public virtual async Task<UnitDto> CreateAsync(UnitCreateDto input)
    {
        var unit = await _unitManager.CreateAsync(input.Code, input.Name, input.SortOrder, input.IsActive);
        return ObjectMapper.Map<Unit, UnitDto>(unit);
    }

    [Authorize(HCPermissions.MasterDatas.UnitEdit)]
    public virtual async Task<UnitDto> UpdateAsync(Guid id, UnitUpdateDto input)
    {
        var unit = await _unitManager.UpdateAsync(id, input.Code, input.Name, input.SortOrder, input.IsActive, input.ConcurrencyStamp);
        return ObjectMapper.Map<Unit, UnitDto>(unit);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(UnitExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _unitRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<Unit>, List<UnitExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Units.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.UnitDelete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> unitIds)
    {
        await _unitRepository.DeleteManyAsync(unitIds);
    }

    [Authorize(HCPermissions.MasterDatas.UnitDelete)]
    public virtual async Task DeleteAllAsync(GetUnitsInput input)
    {
        await _unitRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.SortOrderMin, input.SortOrderMax, input.IsActive);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new UnitDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}