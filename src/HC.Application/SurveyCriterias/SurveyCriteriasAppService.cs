using HC.Shared;
using HC.SurveyLocations;
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
using HC.SurveyCriterias;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;

namespace HC.SurveyCriterias;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.SurveyCriteriaDefault)]
public abstract class SurveyCriteriasAppServiceBase : HCAppService
{
    protected IDistributedCache<SurveyCriteriaDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISurveyCriteriaRepository _surveyCriteriaRepository;
    protected SurveyCriteriaManager _surveyCriteriaManager;
    protected IRepository<HC.SurveyLocations.SurveyLocation, Guid> _surveyLocationRepository;

    public SurveyCriteriasAppServiceBase(ISurveyCriteriaRepository surveyCriteriaRepository, SurveyCriteriaManager surveyCriteriaManager, IDistributedCache<SurveyCriteriaDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.SurveyLocations.SurveyLocation, Guid> surveyLocationRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _surveyCriteriaRepository = surveyCriteriaRepository;
        _surveyCriteriaManager = surveyCriteriaManager;
        _surveyLocationRepository = surveyLocationRepository;
    }

    public virtual async Task<PagedResultDto<SurveyCriteriaWithNavigationPropertiesDto>> GetListAsync(GetSurveyCriteriasInput input)
    {
        var totalCount = await _surveyCriteriaRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.Image, input.DisplayOrderMin, input.DisplayOrderMax, input.IsActive, input.SurveyLocationId);
        var items = await _surveyCriteriaRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.Image, input.DisplayOrderMin, input.DisplayOrderMax, input.IsActive, input.SurveyLocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SurveyCriteriaWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SurveyCriteriaWithNavigationProperties>, List<SurveyCriteriaWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<SurveyCriteriaWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyCriteriaWithNavigationProperties, SurveyCriteriaWithNavigationPropertiesDto>(await _surveyCriteriaRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<SurveyCriteriaDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyCriteria, SurveyCriteriaDto>(await _surveyCriteriaRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input)
    {
        var query = (await _surveyLocationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        query = query.WhereIf(input.IsActive.HasValue, x => x.IsActive == input.IsActive);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.SurveyLocations.SurveyLocation>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.SurveyLocations.SurveyLocation>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.MasterDatas.SurveyCriteriaDelete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _surveyCriteriaRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.MasterDatas.SurveyCriteriaCreate)]
    public virtual async Task<SurveyCriteriaDto> CreateAsync(SurveyCriteriaCreateDto input)
    {
        if (input.SurveyLocationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyLocation"]]);
        }

        var surveyCriteria = await _surveyCriteriaManager.CreateAsync(input.SurveyLocationId, input.Code, input.Name, input.Image, input.DisplayOrder, input.IsActive);
        return ObjectMapper.Map<SurveyCriteria, SurveyCriteriaDto>(surveyCriteria);
    }

    [Authorize(HCPermissions.MasterDatas.SurveyCriteriaEdit)]
    public virtual async Task<SurveyCriteriaDto> UpdateAsync(Guid id, SurveyCriteriaUpdateDto input)
    {
        if (input.SurveyLocationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyLocation"]]);
        }

        var surveyCriteria = await _surveyCriteriaManager.UpdateAsync(id, input.SurveyLocationId, input.Code, input.Name, input.Image, input.DisplayOrder, input.IsActive, input.ConcurrencyStamp);
        return ObjectMapper.Map<SurveyCriteria, SurveyCriteriaDto>(surveyCriteria);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyCriteriaExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var surveyCriterias = await _surveyCriteriaRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.Image, input.DisplayOrderMin, input.DisplayOrderMax, input.IsActive, input.SurveyLocationId);
        var items = surveyCriterias.Select(item => new { Code = item.SurveyCriteria.Code, Name = item.SurveyCriteria.Name, Image = item.SurveyCriteria.Image, DisplayOrder = item.SurveyCriteria.DisplayOrder, IsActive = item.SurveyCriteria.IsActive, SurveyLocation = item.SurveyLocation?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SurveyCriterias.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.SurveyCriteriaDelete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> surveycriteriaIds)
    {
        await _surveyCriteriaRepository.DeleteManyAsync(surveycriteriaIds);
    }

    [Authorize(HCPermissions.MasterDatas.SurveyCriteriaDelete)]
    public virtual async Task DeleteAllAsync(GetSurveyCriteriasInput input)
    {
        await _surveyCriteriaRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.Image, input.DisplayOrderMin, input.DisplayOrderMax, input.IsActive, input.SurveyLocationId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SurveyCriteriaDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}