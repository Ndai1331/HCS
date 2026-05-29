using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Notifications;
using HC.PushNotificationWorker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;

namespace HC.PushNotificationWorker.EventHandlers;

public class NotificationCreatedPushEventHandler : IDistributedEventHandler<NotificationCreatedEto>, ITransientDependency
{
    private const int MaxConcurrency = 50;

    private readonly IRepository<Notification, Guid> _notificationRepository;
    private readonly FirebaseNotificationPushSender _pushSender;
    private readonly ILogger<NotificationCreatedPushEventHandler> _logger;
    private readonly bool _enabled;

    public NotificationCreatedPushEventHandler(
        IRepository<Notification, Guid> notificationRepository,
        FirebaseNotificationPushSender pushSender,
        IConfiguration configuration,
        ILogger<NotificationCreatedPushEventHandler> logger)
    {
        _notificationRepository = notificationRepository;
        _pushSender = pushSender;
        _logger = logger;
        _enabled = configuration.GetValue("PushNotification:Enabled", true);
    }

    public async Task HandleEventAsync(NotificationCreatedEto eventData)
    {
        if (!_enabled)
        {
            _logger.LogTrace("Push notifications disabled via PushNotification:Enabled.");
            return;
        }

        if (eventData.ReceiverUserIds == null || eventData.ReceiverUserIds.Count == 0)
        {
            _logger.LogWarning(
                "NotificationCreatedEto has no receivers: NotificationId={NotificationId}",
                eventData.NotificationId);
            return;
        }

        Notification notification;
        try
        {
            notification = await _notificationRepository.GetAsync(eventData.NotificationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to load notification for FCM: NotificationId={NotificationId}",
                eventData.NotificationId);
            return;
        }

        _logger.LogInformation(
            "FCM notification push: NotificationId={NotificationId}, ReceiverCount={ReceiverCount}, SourceType={SourceType}",
            eventData.NotificationId,
            eventData.ReceiverUserIds.Count,
            notification.SourceType);

        await SendWithThrottlingAsync(notification, eventData.ReceiverUserIds);
    }

    private async Task SendWithThrottlingAsync(Notification notification, List<Guid> receiverUserIds)
    {
        using var semaphore = new SemaphoreSlim(MaxConcurrency);
        var results = await Task.WhenAll(receiverUserIds.Select(async userId =>
        {
            await semaphore.WaitAsync();
            try
            {
                await _pushSender.SendAsync(notification, userId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FCM failed for user {UserId}, notification {NotificationId}", userId, notification.Id);
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }));

        var success = results.Count(r => r);
        var failed = results.Length - success;
        if (failed > 0)
        {
            _logger.LogWarning(
                "Notification FCM partial result: NotificationId={NotificationId}, Success={Success}, Failed={Failed}",
                notification.Id,
                success,
                failed);
        }
    }
}
