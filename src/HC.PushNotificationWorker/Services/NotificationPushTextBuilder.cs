using System;
using System.Globalization;
using System.Linq;
using HC.Localization;
using HC.Notifications;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Localization;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Resolves notification title/content localization keys for FCM display text.
/// </summary>
public class NotificationPushTextBuilder : ITransientDependency
{
    private readonly IStringLocalizer<HCResource> _localizer;
    private readonly ILogger<NotificationPushTextBuilder> _logger;
    private readonly string _cultureName;

    public NotificationPushTextBuilder(
        IStringLocalizer<HCResource> localizer,
        IConfiguration configuration,
        ILogger<NotificationPushTextBuilder> logger)
    {
        _localizer = localizer;
        _logger = logger;
        _cultureName = configuration["PushNotification:DefaultCulture"] ?? "vi";
    }

    public virtual (string Title, string Body) Build(Notification notification)
    {
        using (CultureHelper.Use(_cultureName))
        {
            var title = GetLocalizedTitle(notification.Title);
            var body = TruncateBody(GetLocalizedContent(notification.Content));
            return (title, body);
        }
    }

    private string GetLocalizedTitle(string? titleKey)
    {
        if (string.IsNullOrEmpty(titleKey))
        {
            return string.Empty;
        }

        try
        {
            var localized = _localizer[titleKey];
            return localized.ResourceNotFound ? titleKey : localized.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to localize notification title key {Key}", titleKey);
            return titleKey;
        }
    }

    private string GetLocalizedContent(string? contentKey)
    {
        if (string.IsNullOrEmpty(contentKey))
        {
            return string.Empty;
        }

        var parts = contentKey.Split('|');
        if (parts.Length > 1)
        {
            var key = parts[0];
            var parameters = parts.Skip(1).ToArray();
            try
            {
                var localizedString = _localizer[key];
                if (localizedString.ResourceNotFound || string.IsNullOrEmpty(localizedString.Value))
                {
                    _logger.LogWarning("Localization key not found for notification content: {Key}", key);
                    return contentKey;
                }

                return string.Format(CultureInfo.CurrentCulture, localizedString.Value, parameters);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to localize notification content key {Key}", key);
                return contentKey;
            }
        }

        try
        {
            var localized = _localizer[contentKey];
            return localized.ResourceNotFound ? contentKey : localized.Value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to localize notification content key {Key}", contentKey);
            return contentKey;
        }
    }

    private static string TruncateBody(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return " ";
        }

        const int maxLen = 200;
        return text.Length <= maxLen ? text : text[..maxLen] + "…";
    }
}
