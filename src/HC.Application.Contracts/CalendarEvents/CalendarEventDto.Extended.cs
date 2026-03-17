using System;

namespace HC.CalendarEvents;

public class CalendarEventDto : CalendarEventDtoBase
{
    /// <summary>
    /// Display name of related project or task (e.g. "PRJ001 - Project Name" or "TASK001 - Task Title").
    /// Populated by backend when RelatedType is PROJECT or TASK.
    /// </summary>
    public string? RelatedName { get; set; }

    /// <summary>
    /// Guid of related project or task for navigation. Populated by backend when RelatedType is PROJECT or TASK.
    /// </summary>
    public Guid? RelatedEntityId { get; set; }
}