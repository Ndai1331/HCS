using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.NotificationReceivers;

namespace HC.Controllers.NotificationReceivers;

[RemoteService]
[Area("app")]
[ControllerName("NotificationReceiver")]
[Route("api/app/notification-receivers")]
public class NotificationReceiverController : NotificationReceiverControllerBase, INotificationReceiversAppService
{
    public NotificationReceiverController(INotificationReceiversAppService notificationReceiversAppService) : base(notificationReceiversAppService)
    {
    }

    /// <summary>
    /// Current user's notifications with Title/Content already localized (optional culture query).
    /// </summary>
    [HttpGet]
    [Route("my-localized")]
    public virtual Task<PagedResultDto<NotificationReceiverWithLocalizedNotificationDto>> GetMyListWithLocalizedMessagesAsync(
        [FromQuery] GetMyNotificationsInput input)
    {
        return _notificationReceiversAppService.GetMyListWithLocalizedMessagesAsync(input);
    }
}