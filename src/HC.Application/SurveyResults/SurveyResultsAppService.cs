using HC.Shared;
using HC.SurveySessions;
using HC.SurveyCriterias;
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
using HC.SurveyResults;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.SurveyResults;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.SurveyResults.Default)]
public abstract class SurveyResultsAppServiceBase : HCAppService
{
    protected IDistributedCache<SurveyResultDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISurveyResultRepository _surveyResultRepository;
    protected SurveyResultManager _surveyResultManager;
    protected IRepository<HC.SurveyCriterias.SurveyCriteria, Guid> _surveyCriteriaRepository;
    protected IRepository<HC.SurveySessions.SurveySession, Guid> _surveySessionRepository;

    public SurveyResultsAppServiceBase(ISurveyResultRepository surveyResultRepository, SurveyResultManager surveyResultManager, IDistributedCache<SurveyResultDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.SurveyCriterias.SurveyCriteria, Guid> surveyCriteriaRepository, IRepository<HC.SurveySessions.SurveySession, Guid> surveySessionRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _surveyResultRepository = surveyResultRepository;
        _surveyResultManager = surveyResultManager;
        _surveyCriteriaRepository = surveyCriteriaRepository;
        _surveySessionRepository = surveySessionRepository;
    }

    public virtual async Task<PagedResultDto<SurveyResultWithNavigationPropertiesDto>> GetListAsync(GetSurveyResultsInput input)
    {
        var totalCount = await _surveyResultRepository.GetCountAsync(input.FilterText, input.RatingMin, input.RatingMax, input.SurveyCriteriaId, input.SurveySessionId);
        var items = await _surveyResultRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.RatingMin, input.RatingMax, input.SurveyCriteriaId, input.SurveySessionId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SurveyResultWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SurveyResultWithNavigationProperties>, List<SurveyResultWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<SurveyResultWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyResultWithNavigationProperties, SurveyResultWithNavigationPropertiesDto>(await _surveyResultRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<SurveyResultDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SurveyResult, SurveyResultDto>(await _surveyResultRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSurveyCriteriaLookupAsync(LookupRequestDto input)
    {
        var query = (await _surveyCriteriaRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.SurveyCriterias.SurveyCriteria>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.SurveyCriterias.SurveyCriteria>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input)
    {
        var query = (await _surveySessionRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.SessionDisplay != null && x.SessionDisplay.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.SurveySessions.SurveySession>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.SurveySessions.SurveySession>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.SurveyResults.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _surveyResultRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.SurveyResults.Create)]
    public virtual async Task<SurveyResultDto> CreateAsync(SurveyResultCreateDto input)
    {
        if (input.SurveyCriteriaId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyCriteria"]]);
        }

        if (input.SurveySessionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveySession"]]);
        }

        var surveyResult = await _surveyResultManager.CreateAsync(input.SurveyCriteriaId, input.SurveySessionId, input.Rating);
        return ObjectMapper.Map<SurveyResult, SurveyResultDto>(surveyResult);
    }

    [Authorize(HCPermissions.SurveyResults.Edit)]
    public virtual async Task<SurveyResultDto> UpdateAsync(Guid id, SurveyResultUpdateDto input)
    {
        if (input.SurveyCriteriaId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyCriteria"]]);
        }

        if (input.SurveySessionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveySession"]]);
        }

        var surveyResult = await _surveyResultManager.UpdateAsync(id, input.SurveyCriteriaId, input.SurveySessionId, input.Rating, input.ConcurrencyStamp);
        return ObjectMapper.Map<SurveyResult, SurveyResultDto>(surveyResult);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyResultExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var surveyResults = await _surveyResultRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.RatingMin, input.RatingMax, input.SurveyCriteriaId, input.SurveySessionId);
        var items = surveyResults.Select(item => new { Rating = item.SurveyResult.Rating, SurveyCriteria = item.SurveyCriteria?.Name, SurveySession = item.SurveySession?.SessionDisplay, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SurveyResults.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.SurveyResults.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> surveyresultIds)
    {
        await _surveyResultRepository.DeleteManyAsync(surveyresultIds);
    }

    [Authorize(HCPermissions.SurveyResults.Delete)]
    public virtual async Task DeleteAllAsync(GetSurveyResultsInput input)
    {
        await _surveyResultRepository.DeleteAllAsync(input.FilterText, input.RatingMin, input.RatingMax, input.SurveyCriteriaId, input.SurveySessionId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SurveyResultDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}