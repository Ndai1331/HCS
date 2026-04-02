using System;

namespace HC.CalendarEventParticipants;

public class CalendarEventParticipantCountByEventDto
{
    public Guid CalendarEventId { get; set; }

    public int Count { get; set; }
}
