using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseAdmin;
using HC.Chat.Messages;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Sends FCM notifications for chat messages using device tokens stored per user.
/// </summary>
public class FirebaseChatPushSender : ITransientDependency
{
    private readonly FirebasePushDeliveryService _delivery;
    private readonly ILogger<FirebaseChatPushSender> _logger;

    public FirebaseChatPushSender(
        FirebasePushDeliveryService delivery,
        ILogger<FirebaseChatPushSender> logger)
    {
        _delivery = delivery;
        _logger = logger;
    }

    public virtual async Task SendChatMessageAsync(ChatMessageEto evt)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogTrace("Skipping FCM: Firebase is not initialized.");
            return;
        }

        var title = BuildTitle(evt);
        var body = TruncateBody(evt.Message);
        var collapseKey = evt.ConversationId.HasValue
            ? $"chat-{evt.ConversationId}"
            : $"chat-msg-{evt.MessageId}";

        var data = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["type"] = "chat",
            ["messageId"] = evt.MessageId.ToString(),
            ["conversationId"] = evt.ConversationId?.ToString() ?? "",
            ["conversationType"] = evt.ConversationType ?? "",
            ["senderUserId"] = evt.SenderUserId.ToString()
        };

        _logger.LogDebug(
            "Sending chat FCM: MessageId={MessageId}, TargetUserId={TargetUserId}",
            evt.MessageId,
            evt.TargetUserId);

        await _delivery.SendToUserAsync(evt.TargetUserId, title, body, data, collapseKey);
    }

    private static string BuildTitle(ChatMessageEto evt)
    {
        var name = $"{evt.SenderName} {evt.SenderSurname}".Trim();
        if (string.IsNullOrEmpty(name))
        {
            name = evt.SenderUserName ?? "Chat";
        }

        if (!string.IsNullOrWhiteSpace(evt.ConversationName))
        {
            return $"{name} ({evt.ConversationName})";
        }

        return name;
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
