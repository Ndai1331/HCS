using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FirebaseAdmin;
using FcmMessage = FirebaseAdmin.Messaging;
using HC.PushNotifications;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.PushNotificationWorker.Services;

/// <summary>
/// Shared FCM delivery: token lookup, send, deactivate invalid tokens.
/// </summary>
public class FirebasePushDeliveryService : ITransientDependency
{
    private readonly IRepository<UserPushDeviceToken, Guid> _tokenRepository;
    private readonly ILogger<FirebasePushDeliveryService> _logger;

    public FirebasePushDeliveryService(
        IRepository<UserPushDeviceToken, Guid> tokenRepository,
        ILogger<FirebasePushDeliveryService> logger)
    {
        _tokenRepository = tokenRepository;
        _logger = logger;
    }

    public virtual async Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        string collapseKey)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogTrace("Skipping FCM: Firebase is not initialized.");
            return;
        }

        var devices = await _tokenRepository.GetListAsync(x => x.UserId == userId && x.IsActive);
        if (devices.Count == 0)
        {
            _logger.LogTrace("No active FCM tokens for user {UserId}.", userId);
            return;
        }

        await SendToDevicesAsync(devices, title, body, data, collapseKey);
    }

    public virtual async Task SendToDevicesAsync(
        IReadOnlyList<UserPushDeviceToken> devices,
        string title,
        string body,
        IReadOnlyDictionary<string, string> data,
        string collapseKey)
    {
        if (FirebaseApp.DefaultInstance == null)
        {
            _logger.LogTrace("Skipping FCM: Firebase is not initialized.");
            return;
        }

        if (devices.Count == 0)
        {
            return;
        }

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
                    Data = new Dictionary<string, string>(data, StringComparer.Ordinal),
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
                    "FCM aborted: Firebase service account credentials are invalid. "
                    + "Regenerate the service account JSON key and restart the worker.");
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
}
