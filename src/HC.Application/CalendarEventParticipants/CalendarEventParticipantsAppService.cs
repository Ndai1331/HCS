using HC.Shared;
using Volo.Abp.Identity;
using HC.CalendarEvents;
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
using HC.CalendarEventParticipants;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.CalendarEventParticipants;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.CalendarEventParticipants.Default)]
public abstract class CalendarEventParticipantsAppServiceBase : HCAppService
{
    protected IDistributedCache<CalendarEventParticipantDownloadTokenCacheItem, string> _downloadTokenCache;
    protected ICalendarEventParticipantRepository _calendarEventParticipantRepository;
    protected CalendarEventParticipantManager _calendarEventParticipantManager;
    protected IRepository<HC.CalendarEvents.CalendarEvent, Guid> _calendarEventRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public CalendarEventParticipantsAppServiceBase(ICalendarEventParticipantRepository calendarEventParticipantRepository, CalendarEventParticipantManager calendarEventParticipantManager, IDistributedCache<CalendarEventParticipantDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.CalendarEvents.CalendarEvent, Guid> calendarEventRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _calendarEventParticipantRepository = calendarEventParticipantRepository;
        _calendarEventParticipantManager = calendarEventParticipantManager;
        _calendarEventRepository = calendarEventRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<CalendarEventParticipantWithNavigationPropertiesDto>> GetListAsync(GetCalendarEventParticipantsInput input)
    {
        var totalCount = await _calendarEventParticipantRepository.GetCountAsync(input.FilterText, input.ResponseStatus, input.Notified, input.CalendarEventId, input.IdentityUserId);
        var items = await _calendarEventParticipantRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.ResponseStatus, input.Notified, input.CalendarEventId, input.IdentityUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        // Manual mapping to handle null CalendarEvent/IdentityUser (orphaned participants from LEFT JOIN)
        var dtos = items.Select(item => new CalendarEventParticipantWithNavigationPropertiesDto
        {
            CalendarEventParticipant = ObjectMapper.Map<CalendarEventParticipant, CalendarEventParticipantDto>(item.CalendarEventParticipant),
            CalendarEvent = item.CalendarEvent == null ? null : ObjectMapper.Map<CalendarEvent, CalendarEventDto>(item.CalendarEvent),
            IdentityUser = item.IdentityUser == null ? null : ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, Volo.Abp.Identity.IdentityUserDto>(item.IdentityUser)
        }).ToList();
        return new PagedResultDto<CalendarEventParticipantWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = dtos
        };
    }

    public virtual async Task<CalendarEventParticipantWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _calendarEventParticipantRepository.GetWithNavigationPropertiesAsync(id);
        if (item == null) throw new Volo.Abp.BusinessException("HC:CalendarEventParticipantNotFound");
        return new CalendarEventParticipantWithNavigationPropertiesDto
        {
            CalendarEventParticipant = ObjectMapper.Map<CalendarEventParticipant, CalendarEventParticipantDto>(item.CalendarEventParticipant),
            CalendarEvent = item.CalendarEvent == null ? null : ObjectMapper.Map<CalendarEvent, CalendarEventDto>(item.CalendarEvent),
            IdentityUser = item.IdentityUser == null ? null : ObjectMapper.Map<Volo.Abp.Identity.IdentityUser, Volo.Abp.Identity.IdentityUserDto>(item.IdentityUser)
        };
    }

    public virtual async Task<CalendarEventParticipantDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<CalendarEventParticipant, CalendarEventParticipantDto>(await _calendarEventParticipantRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetCalendarEventLookupAsync(LookupRequestDto input)
    {
        var query = (await _calendarEventRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title != null && x.Title.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.CalendarEvents.CalendarEvent>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.CalendarEvents.CalendarEvent>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.CalendarEventParticipants.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _calendarEventParticipantRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.CalendarEventParticipants.Create)]
    public virtual async Task<CalendarEventParticipantDto> CreateAsync(CalendarEventParticipantCreateDto input)
    {
        if (input.CalendarEventId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["CalendarEvent"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var calendarEventParticipant = await _calendarEventParticipantManager.CreateAsync(input.CalendarEventId, input.IdentityUserId, input.ResponseStatus, input.Notified);
        return ObjectMapper.Map<CalendarEventParticipant, CalendarEventParticipantDto>(calendarEventParticipant);
    }

    [Authorize(HCPermissions.CalendarEventParticipants.Edit)]
    public virtual async Task<CalendarEventParticipantDto> UpdateAsync(Guid id, CalendarEventParticipantUpdateDto input)
    {
        if (input.CalendarEventId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["CalendarEvent"]]);
        }

        if (input.IdentityUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var calendarEventParticipant = await _calendarEventParticipantManager.UpdateAsync(id, input.CalendarEventId, input.IdentityUserId, input.ResponseStatus, input.Notified, input.ConcurrencyStamp);
        return ObjectMapper.Map<CalendarEventParticipant, CalendarEventParticipantDto>(calendarEventParticipant);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(CalendarEventParticipantExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var calendarEventParticipants = await _calendarEventParticipantRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.ResponseStatus, input.Notified, input.CalendarEventId, input.IdentityUserId);
        var items = calendarEventParticipants.Select(item => new { ResponseStatus = item.CalendarEventParticipant.ResponseStatus, Notified = item.CalendarEventParticipant.Notified, CalendarEvent = item.CalendarEvent?.Title, IdentityUser = item.IdentityUser?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "CalendarEventParticipants.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.CalendarEventParticipants.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> calendareventparticipantIds)
    {
        await _calendarEventParticipantRepository.DeleteManyAsync(calendareventparticipantIds);
    }

    [Authorize(HCPermissions.CalendarEventParticipants.Delete)]
    public virtual async Task DeleteAllAsync(GetCalendarEventParticipantsInput input)
    {
        await _calendarEventParticipantRepository.DeleteAllAsync(input.FilterText, input.ResponseStatus, input.Notified, input.CalendarEventId, input.IdentityUserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new CalendarEventParticipantDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}