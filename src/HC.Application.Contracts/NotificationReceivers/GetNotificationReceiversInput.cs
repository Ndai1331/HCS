using Volo.Abp.Application.Dtos;
using System;

namespace HC.NotificationReceivers;

public abstract class GetNotificationReceiversInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public bool? IsRead { get; set; }

    public string? SourceType { get; set; }

    public DateTime? ReadAtMin { get; set; }

    public DateTime? ReadAtMax { get; set; }

    public Guid? NotificationId { get; set; }

    public Guid? IdentityUserId { get; set; }
    public DateTime? CreationTimeMin { get; set; }
    public DateTime? CreationTimeMax { get; set; }

    public GetNotificationReceiversInputBase()
    {
    }
}