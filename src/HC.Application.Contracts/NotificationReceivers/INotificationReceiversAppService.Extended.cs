using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace HC.NotificationReceivers;

public partial interface INotificationReceiversAppService
{
    Task MarkAllAsReadAsync(string? sourceType = null);
    Task MarkAsReadAsync(Guid notificationId);

    /// <summary>
    /// Returns notifications for the current user with Title/Content resolved using HC localization
    /// and optional <see cref="GetMyNotificationsInput.Culture"/>.
    /// </summary>
    Task<PagedResultDto<NotificationReceiverWithLocalizedNotificationDto>> GetMyListWithLocalizedMessagesAsync(
        GetMyNotificationsInput input);
}