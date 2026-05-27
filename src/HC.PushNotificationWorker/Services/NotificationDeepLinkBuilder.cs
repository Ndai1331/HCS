using System;
using HC.Notifications;
using Volo.Abp.DependencyInjection;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Builds in-app deep link paths aligned with Blazor Notification.razor routing.
/// </summary>
public class NotificationDeepLinkBuilder : ITransientDependency
{
    public virtual string Build(Notification notification)
    {
        if (string.IsNullOrEmpty(notification.RelatedId))
        {
            return "#";
        }

        var related = notification.RelatedType?.ToUpperInvariant() ?? string.Empty;
        if (related == nameof(RelatedType.APPROVAL_DOCUMENT))
        {
            // SentToMe = 2 (DocumentSourceType)
            return $"/manage-documents?sourceType=2&relatedId={Uri.EscapeDataString(notification.RelatedId)}";
        }

        if (string.IsNullOrEmpty(notification.RelatedType))
        {
            return "#";
        }

        return related switch
        {
            nameof(RelatedType.WORKFLOW) => $"/document-signing/{notification.RelatedId}",
            nameof(RelatedType.TASK) => $"/project-task-detail/{notification.RelatedId}",
            nameof(RelatedType.PROJECT) => $"/project-detail/{notification.RelatedId}",
            nameof(RelatedType.DOCUMENT) => $"/view-document-detail/{notification.RelatedId}?sourceType=1",
            nameof(RelatedType.CALENDAR_EVENT) => $"/calendar-event-detail/{notification.RelatedId}",
            _ => "#"
        };
    }
}
