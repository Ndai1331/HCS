namespace HC.NotificationReceivers;

/// <summary>
/// Same shape as <see cref="NotificationReceiverWithNavigationPropertiesDto"/>, but
/// <see cref="NotificationReceiverWithNavigationPropertiesDtoBase.Notification"/>.Title and .Content
/// are human-readable strings for the requested culture (not raw localization keys).
/// </summary>
public class NotificationReceiverWithLocalizedNotificationDto : NotificationReceiverWithNavigationPropertiesDto
{
}
