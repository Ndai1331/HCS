using System;
using System.Collections.Generic;

namespace HC.CalendarEventParticipants;

public class GetCalendarEventParticipantCountsInput
{
    public List<Guid> CalendarEventIds { get; set; } = new();
}
