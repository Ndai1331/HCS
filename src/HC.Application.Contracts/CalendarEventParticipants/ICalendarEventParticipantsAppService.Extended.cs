using System.Collections.Generic;
using System.Threading.Tasks;

namespace HC.CalendarEventParticipants;

public partial interface ICalendarEventParticipantsAppService
{
    /// <summary>
    /// Returns participant counts per calendar event in one query (events with zero participants have Count 0).
    /// </summary>
    Task<List<CalendarEventParticipantCountByEventDto>> CalculateParticipantCountsByCalendarEventIdsAsync(
        GetCalendarEventParticipantCountsInput input);
}