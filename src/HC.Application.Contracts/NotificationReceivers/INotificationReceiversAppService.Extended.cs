using System;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace HC.NotificationReceivers;

public partial interface INotificationReceiversAppService
{
    Task MarkAllAsReadAsync(string? sourceType = null);
    Task MarkAsReadAsync(Guid notificationId);
}