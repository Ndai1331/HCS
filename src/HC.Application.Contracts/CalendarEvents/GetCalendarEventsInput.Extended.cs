using System;

namespace HC.CalendarEvents;

public class GetCalendarEventsInput : GetCalendarEventsInputBase
{
    public Guid? UserId { get; set; }
}