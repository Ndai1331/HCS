using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.CalendarEventParticipants;

namespace HC.Controllers.CalendarEventParticipants;

[RemoteService]
[Area("app")]
[ControllerName("CalendarEventParticipant")]
[Route("api/app/calendar-event-participants")]
public class CalendarEventParticipantController : CalendarEventParticipantControllerBase, ICalendarEventParticipantsAppService
{
    public CalendarEventParticipantController(ICalendarEventParticipantsAppService calendarEventParticipantsAppService) : base(calendarEventParticipantsAppService)
    {
    }

    [HttpPost]
    [Route("participant-counts-by-calendar-event-ids")]
    public virtual Task<List<CalendarEventParticipantCountByEventDto>> CalculateParticipantCountsByCalendarEventIdsAsync(
        GetCalendarEventParticipantCountsInput input)
    {
        return _calendarEventParticipantsAppService.CalculateParticipantCountsByCalendarEventIdsAsync(input);
    }
}