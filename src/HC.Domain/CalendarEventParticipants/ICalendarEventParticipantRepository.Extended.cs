using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HC.CalendarEventParticipants;

public partial interface ICalendarEventParticipantRepository
{
    Task<IReadOnlyList<(Guid CalendarEventId, int Count)>> GetCountsByCalendarEventIdsAsync(
        IReadOnlyList<Guid> calendarEventIds,
        CancellationToken cancellationToken = default);
}