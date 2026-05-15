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
using HC.SurveyLocations;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.SurveyLocations;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.Default)]
public abstract class SurveyLocationsAppServiceBase : HCAppService
{
    protected IDistributedCache<SurveyLocationDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISurveyLocationRepository _surveyLocationRepository;
    protected SurveyLocationManager _surveyLocationManager;

    public SurveyLocationsAppServiceBase(ISurveyLocationRepository surveyLocationRepository, SurveyLocationManager surveyLocationManager, IDistributedCache<SurveyLocationDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _surveyLocationRepository = surveyLocationRepository;
        _surveyLocationManager = surveyLocationManager;
    }

    public virtual async Task<PagedResultDto<SurveyLocationDto>> GetListAsync(GetSurveyLocationsInput input)
    {
        var totalCount = await _surveyLocationRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
        var items = await _surveyLocationRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SurveyLocationDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SurveyLocation>, List<SurveyLocationDto>>(items)
        };
    }

    public virtual async Task<SurveyLocationDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyLocation, SurveyLocationDto>(await _surveyLocationRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _surveyLocationRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.MasterDatas.Create)]
    public virtual async Task<SurveyLocationDto> CreateAsync(SurveyLocationCreateDto input)
    {
        var surveyLocation = await _surveyLocationManager.CreateAsync(input.Code, input.Name, input.IsActive, input.Description);
        return ObjectMapper.Map<SurveyLocation, SurveyLocationDto>(surveyLocation);
    }

    [Authorize(HCPermissions.MasterDatas.Edit)]
    public virtual async Task<SurveyLocationDto> UpdateAsync(Guid id, SurveyLocationUpdateDto input)
    {
        var surveyLocation = await _surveyLocationManager.UpdateAsync(id, input.Code, input.Name, input.IsActive, input.Description, input.ConcurrencyStamp);
        return ObjectMapper.Map<SurveyLocation, SurveyLocationDto>(surveyLocation);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyLocationExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _surveyLocationRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<SurveyLocation>, List<SurveyLocationExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SurveyLocations.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> surveylocationIds)
    {
        await _surveyLocationRepository.DeleteManyAsync(surveylocationIds);
    }

    [Authorize(HCPermissions.MasterDatas.Delete)]
    public virtual async Task DeleteAllAsync(GetSurveyLocationsInput input)
    {
        await _surveyLocationRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SurveyLocationDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}