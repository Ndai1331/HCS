using System;
using Volo.Abp.Application.Dtos;

namespace HC.NotificationReceivers;

/// <summary>
/// Query for the current user's notification receivers with localized notification text.
/// User is always taken from the authenticated context (never from this DTO).
/// </summary>
public class GetMyNotificationsInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public bool? IsRead { get; set; }

    public string? SourceType { get; set; }

    public DateTime? ReadAtMin { get; set; }

    public DateTime? ReadAtMax { get; set; }

    public Guid? NotificationId { get; set; }

    public DateTime? CreationTimeMin { get; set; }

    public DateTime? CreationTimeMax { get; set; }

    /// <summary>
    /// Optional UI culture (e.g. "vi", "en"). When omitted, the current request/thread UI culture is used.
    /// </summary>
    public string? Culture { get; set; }
}
