using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseAdmin;
using FcmMessage = FirebaseAdmin.Messaging;
using HC.Chat.Messages;
using HC.PushNotifications;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Sends FCM notifications for chat messages using device tokens stored per user.
/// </summary>
public class FirebaseChatPushSender : ITransientDependency
{
    private readonly IRepository<UserPushDeviceToken, Guid> _tokenRepository;
    private readonly ILogger<FirebaseChatPushSender> _logger;

    public FirebaseChatPushSender(
        IRepository<UserPushDeviceToken, Guid> tokenRepository,
        ILogger<FirebaseChatPushSender> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public virtual async Task SendChatMessageAsync(ChatMessageEto evt)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogTrace("Skipping FCM: Firebase is not initialized.");
            return;
        }

        var devices = await _tokenRepository.GetListAsync(x => x.UserId == evt.TargetUserId && x.IsActive);
        if (devices.Count == 0)
        {
            _logger.LogTrace("No active FCM tokens for user {UserId}.", evt.TargetUserId);
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

        var messaging = FcmMessage.FirebaseMessaging.GetMessaging(FirebaseApp.DefaultInstance);

        foreach (var device in devices)
        {
            try
            {
                var message = new FcmMessage.Message
                {
                    Token = device.FcmToken,
                    Notification = new FcmMessage.Notification
                    {
                        Title = title,
                        Body = body
                    },
                    Data = data,
                    Android = new FcmMessage.AndroidConfig
                    {
                        Priority = FcmMessage.Priority.High,
                        CollapseKey = collapseKey
                    }
                };

                await messaging.SendAsync(message);
            }
            catch (FcmMessage.FirebaseMessagingException ex)
            {
                _logger.LogWarning(ex, "FCM send failed for device token id {DeviceId}", device.Id);
                if (ShouldDeactivateToken(ex))
                {
                    device.Deactivate();
                    await _tokenRepository.UpdateAsync(device, autoSave: true);
                }
            }
            catch (Exception ex) when (FirebaseCredentialHelper.IsCredentialError(ex))
            {
                _logger.LogError(
                    ex,
                    "FCM aborted: Firebase service account credentials are invalid (invalid_grant / Invalid JWT Signature). "
                    + "This affects all devices — regenerate the service account JSON key on the server and restart the worker.");
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Unexpected error sending FCM to device {DeviceId}", device.Id);
            }
        }
    }

    private static bool ShouldDeactivateToken(FcmMessage.FirebaseMessagingException ex)
    {
        return ex.MessagingErrorCode == FcmMessage.MessagingErrorCode.Unregistered
               || ex.MessagingErrorCode == FcmMessage.MessagingErrorCode.InvalidArgument;
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
