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
using HC.CalendarEvents;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.CalendarEvents;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.CalendarEvents.Default)]
public abstract class CalendarEventsAppServiceBase : HCAppService
{
    protected IDistributedCache<CalendarEventDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ICalendarEventRepository _calendarEventRepository;
    protected CalendarEventManager _calendarEventManager;

    public CalendarEventsAppServiceBase(ICalendarEventRepository calendarEventRepository, CalendarEventManager calendarEventManager, IDistributedCache<CalendarEventDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _calendarEventRepository = calendarEventRepository;
        _calendarEventManager = calendarEventManager;
    }

    public virtual async Task<PagedResultDto<CalendarEventDto>> GetListAsync(GetCalendarEventsInput input)
    {
        var totalCount = await _calendarEventRepository.GetCountAsync(input.FilterText, input.Title, input.Description, input.StartTimeMin, input.StartTimeMax, input.EndTimeMin, input.EndTimeMax, input.AllDay, input.EventType, input.Location, input.RelatedType, input.RelatedId, input.Visibility);
        var items = await _calendarEventRepository.GetListAsync(input.FilterText, input.Title, input.Description, input.StartTimeMin, input.StartTimeMax, input.EndTimeMin, input.EndTimeMax, input.AllDay, input.EventType, input.Location, input.RelatedType, input.RelatedId, input.Visibility, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<CalendarEventDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<CalendarEvent>, List<CalendarEventDto>>(items)
        };
    }

    public virtual async Task<CalendarEventDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<CalendarEvent, CalendarEventDto>(await _calendarEventRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.CalendarEvents.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _calendarEventRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.CalendarEvents.Create)]
    public virtual async Task<CalendarEventDto> CreateAsync(CalendarEventCreateDto input)
    {
        var calendarEvent = await _calendarEventManager.CreateAsync(input.Title, input.StartTime, input.EndTime, input.AllDay, input.EventType, input.RelatedType, input.Visibility, input.Description, input.Location, input.RelatedId);
        return ObjectMapper.Map<CalendarEvent, CalendarEventDto>(calendarEvent);
    }

    [Authorize(HCPermissions.CalendarEvents.Edit)]
    public virtual async Task<CalendarEventDto> UpdateAsync(Guid id, CalendarEventUpdateDto input)
    {
        var calendarEvent = await _calendarEventManager.UpdateAsync(id, input.Title, input.StartTime, input.EndTime, input.AllDay, input.EventType, input.RelatedType, input.Visibility, input.Description, input.Location, input.RelatedId, input.ConcurrencyStamp);
        return ObjectMapper.Map<CalendarEvent, CalendarEventDto>(calendarEvent);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(CalendarEventExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _calendarEventRepository.GetListAsync(input.FilterText, input.Title, input.Description, input.StartTimeMin, input.StartTimeMax, input.EndTimeMin, input.EndTimeMax, input.AllDay, input.EventType, input.Location, input.RelatedType, input.RelatedId, input.Visibility);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<CalendarEvent>, List<CalendarEventExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "CalendarEvents.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.CalendarEvents.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> calendareventIds)
    {
        await _calendarEventRepository.DeleteManyAsync(calendareventIds);
    }

    [Authorize(HCPermissions.CalendarEvents.Delete)]
    public virtual async Task DeleteAllAsync(GetCalendarEventsInput input)
    {
        await _calendarEventRepository.DeleteAllAsync(input.FilterText, input.Title, input.Description, input.StartTimeMin, input.StartTimeMax, input.EndTimeMin, input.EndTimeMax, input.AllDay, input.EventType, input.Location, input.RelatedType, input.RelatedId, input.Visibility);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new CalendarEventDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}