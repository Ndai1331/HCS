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
using HC.SurveySessions;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.SurveySessions;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.SurveySessions.Default)]
public abstract class SurveySessionsAppServiceBase : HCAppService
{
    protected IDistributedCache<SurveySessionDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ISurveySessionRepository _surveySessionRepository;
    protected SurveySessionManager _surveySessionManager;
    protected IRepository<HC.SurveyLocations.SurveyLocation, Guid> _surveyLocationRepository;

    public SurveySessionsAppServiceBase(ISurveySessionRepository surveySessionRepository, SurveySessionManager surveySessionManager, IDistributedCache<SurveySessionDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.SurveyLocations.SurveyLocation, Guid> surveyLocationRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _surveySessionRepository = surveySessionRepository;
        _surveySessionManager = surveySessionManager;
        _surveyLocationRepository = surveyLocationRepository;
    }

    public virtual async Task<PagedResultDto<SurveySessionWithNavigationPropertiesDto>> GetListAsync(GetSurveySessionsInput input)
    {
        var deviceTypeFilter = input.DeviceType?.ToString();
        var totalCount = await _surveySessionRepository.GetCountAsync(input.FilterText, input.FullName, input.PhoneNumber, input.PatientCode, input.SurveyTimeMin, input.SurveyTimeMax, deviceTypeFilter, input.Note, input.SessionDisplay, input.SurveyLocationId);
        var items = await _surveySessionRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FullName, input.PhoneNumber, input.PatientCode, input.SurveyTimeMin, input.SurveyTimeMax, deviceTypeFilter, input.Note, input.SessionDisplay, input.SurveyLocationId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<SurveySessionWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<SurveySessionWithNavigationProperties>, List<SurveySessionWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<SurveySessionWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<SurveySessionWithNavigationProperties, SurveySessionWithNavigationPropertiesDto>(await _surveySessionRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<SurveySessionDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<SurveySession, SurveySessionDto>(await _surveySessionRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input)
    {
        var query = (await _surveyLocationRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.SurveyLocations.SurveyLocation>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.SurveyLocations.SurveyLocation>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.SurveySessions.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _surveySessionRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.SurveySessions.Create)]
    public virtual async Task<SurveySessionDto> CreateAsync(SurveySessionCreateDto input)
    {
        if (input.SurveyLocationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyLocation"]]);
        }

        var sessionDisplay = $"{input.FullName}_{input.PhoneNumber}_{input.SurveyLocationId}_{input.SurveyTime:ddMMyyyyHHmm}";
        var inputDeviceType = input.DeviceType?.ToString();
        var surveySession = await _surveySessionManager.CreateAsync(input.SurveyLocationId, input.SurveyTime, sessionDisplay, input.FullName, input.PhoneNumber, input.PatientCode, inputDeviceType, input.Note);
        return ObjectMapper.Map<SurveySession, SurveySessionDto>(surveySession);
    }

    [Authorize(HCPermissions.SurveySessions.Edit)]
    public virtual async Task<SurveySessionDto> UpdateAsync(Guid id, SurveySessionUpdateDto input)
    {
        if (input.SurveyLocationId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["SurveyLocation"]]);
        }

        var sessionDisplay = $"{input.FullName}_{input.PhoneNumber}_{input.SurveyLocationId}_{input.SurveyTime:ddMMyyyyHHmm}";
        var inputDeviceType = input.DeviceType?.ToString();
        var surveySession = await _surveySessionManager.UpdateAsync(id, input.SurveyLocationId, input.SurveyTime, sessionDisplay, input.FullName, input.PhoneNumber, input.PatientCode, inputDeviceType, input.Note, input.ConcurrencyStamp);
        return ObjectMapper.Map<SurveySession, SurveySessionDto>(surveySession);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveySessionExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var surveySessions = await _surveySessionRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.FullName, input.PhoneNumber, input.PatientCode, input.SurveyTimeMin, input.SurveyTimeMax, input.DeviceType, input.Note, input.SessionDisplay, input.SurveyLocationId);
        var items = surveySessions.Select(item => new { FullName = item.SurveySession.FullName, PhoneNumber = item.SurveySession.PhoneNumber, PatientCode = item.SurveySession.PatientCode, SurveyTime = item.SurveySession.SurveyTime, DeviceType = item.SurveySession.DeviceType, Note = item.SurveySession.Note, SessionDisplay = item.SurveySession.SessionDisplay, SurveyLocation = item.SurveyLocation?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "SurveySessions.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.SurveySessions.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> surveysessionIds)
    {
        await _surveySessionRepository.DeleteManyAsync(surveysessionIds);
    }

    [Authorize(HCPermissions.SurveySessions.Delete)]
    public virtual async Task DeleteAllAsync(GetSurveySessionsInput input)
    {
        var deviceTypeFilter = input.DeviceType?.ToString();
        await _surveySessionRepository.DeleteAllAsync(input.FilterText, input.FullName, input.PhoneNumber, input.PatientCode, input.SurveyTimeMin, input.SurveyTimeMax, deviceTypeFilter, input.Note, input.SessionDisplay, input.SurveyLocationId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new SurveySessionDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}