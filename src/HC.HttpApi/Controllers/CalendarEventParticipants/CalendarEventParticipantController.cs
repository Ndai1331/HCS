using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.CalendarEventParticipants;
using Volo.Abp.Content;

namespace HC.Controllers.CalendarEventParticipants;

[RemoteService]
[Area("app")]
[ControllerName("CalendarEventParticipant")]
[Route("api/app/calendar-event-participants")]
public abstract class CalendarEventParticipantControllerBase : AbpController
{
    protected ICalendarEventParticipantsAppService _calendarEventParticipantsAppService;

    public CalendarEventParticipantControllerBase(ICalendarEventParticipantsAppService calendarEventParticipantsAppService)
    {
        _calendarEventParticipantsAppService = calendarEventParticipantsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<CalendarEventParticipantWithNavigationPropertiesDto>> GetListAsync(GetCalendarEventParticipantsInput input)
    {
        return _calendarEventParticipantsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<CalendarEventParticipantWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _calendarEventParticipantsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<CalendarEventParticipantDto> GetAsync(Guid id)
    {
        return _calendarEventParticipantsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("calendar-event-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetCalendarEventLookupAsync(LookupRequestDto input)
    {
        return _calendarEventParticipantsAppService.GetCalendarEventLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _calendarEventParticipantsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<CalendarEventParticipantDto> CreateAsync(CalendarEventParticipantCreateDto input)
    {
        return _calendarEventParticipantsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<CalendarEventParticipantDto> UpdateAsync(Guid id, CalendarEventParticipantUpdateDto input)
    {
        return _calendarEventParticipantsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _calendarEventParticipantsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(CalendarEventParticipantExcelDownloadDto input)
    {
        return _calendarEventParticipantsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _calendarEventParticipantsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> calendareventparticipantIds)
    {
        return _calendarEventParticipantsAppService.DeleteByIdsAsync(calendareventparticipantIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetCalendarEventParticipantsInput input)
    {
        return _calendarEventParticipantsAppService.DeleteAllAsync(input);
    }
}