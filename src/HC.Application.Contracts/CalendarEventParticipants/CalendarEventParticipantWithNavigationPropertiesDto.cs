using HC.CalendarEvents;
using Volo.Abp.Identity;
using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.CalendarEventParticipants;

public abstract class CalendarEventParticipantWithNavigationPropertiesDtoBase
{
    public CalendarEventParticipantDto CalendarEventParticipant { get; set; } = null!;
    /// <summary>Can be null when participant references a deleted CalendarEvent (orphaned participant).</summary>
    public CalendarEventDto? CalendarEvent { get; set; }
    /// <summary>Can be null when participant references a deleted IdentityUser (orphaned participant).</summary>
    public IdentityUserDto? IdentityUser { get; set; }
}