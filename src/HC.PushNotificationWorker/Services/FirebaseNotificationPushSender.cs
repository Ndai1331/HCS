using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.Notifications;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Sends FCM for in-app notifications (NotificationCreatedEto pipeline).
/// </summary>
public class FirebaseNotificationPushSender : ITransientDependency
{
    private readonly FirebasePushDeliveryService _delivery;
    private readonly NotificationPushTextBuilder _textBuilder;
    private readonly NotificationDeepLinkBuilder _deepLinkBuilder;
    private readonly ILogger<FirebaseNotificationPushSender> _logger;

    public FirebaseNotificationPushSender(
        FirebasePushDeliveryService delivery,
        NotificationPushTextBuilder textBuilder,
        NotificationDeepLinkBuilder deepLinkBuilder,
        ILogger<FirebaseNotificationPushSender> logger)
    {
        _delivery = delivery;
        _textBuilder = textBuilder;
        _deepLinkBuilder = deepLinkBuilder;
        _logger = logger;
    }

    public virtual async Task SendAsync(Notification notification, Guid userId)
    {
        var (title, body) = _textBuilder.Build(notification);
        var deepLink = _deepLinkBuilder.Build(notification);

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = "notification",
            ["notificationId"] = notification.Id.ToString(),
            ["sourceType"] = notification.SourceType ?? "",
            ["relatedType"] = notification.RelatedType ?? "",
            ["relatedId"] = notification.RelatedId ?? "",
            ["eventType"] = notification.EventType ?? "",
            ["priority"] = notification.Priority ?? "",
            ["deepLink"] = deepLink
        };

        _logger.LogDebug(
            "Sending notification FCM: NotificationId={NotificationId}, UserId={UserId}, SourceType={SourceType}",
            notification.Id,
            userId,
            notification.SourceType);

        await _delivery.SendToUserAsync(
            userId,
            title,
            body,
            data,
            $"notif-{notification.Id}");
    }
}
