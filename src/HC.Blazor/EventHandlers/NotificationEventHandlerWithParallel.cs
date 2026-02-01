using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Notifications;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using HC.Blazor.Hubs;
using HC.Blazor.Services;

namespace HC.Blazor.EventHandlers;

/// <summary>
/// Enhanced notification event handler with parallel sending and retry policies
/// </summary>
public class NotificationEventHandlerWithParallel :
    IDistributedEventHandler<NotificationCreatedEto>,
    ITransientDependency
{
    private readonly IHubContext<NotificationHub> _hubContext;
    private readonly ILogger<NotificationEventHandlerWithParallel> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IDeadLetterQueue _deadLetterQueue;

    public NotificationEventHandlerWithParallel(
        IHubContext<NotificationHub> hubContext,
        ILogger<NotificationEventHandlerWithParallel> logger,
        IDeadLetterQueue deadLetterQueue)
    {
        _hubContext = hubContext;
        _logger = logger;
        _deadLetterQueue = deadLetterQueue;
        
        // Initialize retry policy: 3 retries with exponential backoff
        _retryPolicy = new RetryPolicy(
            logger,
            maxRetries: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(30),
            backoffMultiplier: 2.0);
        
        // Initialize circuit breaker: opens after 10 failures (notifications are less critical)
        _circuitBreaker = new CircuitBreaker(
            logger,
            failureThreshold: 10,
            openTimeout: TimeSpan.FromMinutes(1));
    }

    public async Task HandleEventAsync(NotificationCreatedEto eventData)
    {
        try
        {
            _logger.LogInformation(
                "Handling NotificationCreatedEto: NotificationId={NotificationId}, ReceiverCount={ReceiverCount}",
                eventData.NotificationId,
                eventData.ReceiverUserIds?.Count ?? 0);

            if (eventData.ReceiverUserIds == null || eventData.ReceiverUserIds.Count == 0)
            {
                _logger.LogWarning("No receiver user IDs in event data");
                return;
            }

            // Execute with circuit breaker and retry policy
            await _circuitBreaker.ExecuteAsync(
                async () => await _retryPolicy.ExecuteAsync(
                    () => SendNotificationsInParallelAsync(eventData),
                    "SendNotifications"),
                "SendNotifications");

            _logger.LogInformation(
                "Successfully processed notification: NotificationId={NotificationId}",
                eventData.NotificationId);
        }
        catch (CircuitBreakerOpenException ex)
        {
            _logger.LogError(ex, "Circuit breaker is open, adding to dead letter queue");
            await _deadLetterQueue.AddAsync(eventData, "Circuit breaker open", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All retry attempts failed, adding to dead letter queue");
            await _deadLetterQueue.AddAsync(eventData, "Max retries exceeded", ex);
        }
    }

    /// <summary>
    /// Send notifications to all users in parallel for better performance
    /// </summary>
    private async Task SendNotificationsInParallelAsync(NotificationCreatedEto eventData)
    {
        // Create tasks for each user
        var sendTasks = eventData.ReceiverUserIds.Select(async userId =>
        {
            try
            {
                await SendNotificationToUserAsync(userId, eventData.NotificationId);
                return (Success: true, UserId: userId, Error: (Exception)null);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
                return (Success: false, UserId: userId, Error: ex);
            }
        }).ToList();

        // Execute all sends in parallel
        var results = await Task.WhenAll(sendTasks);

        // Log summary
        var successCount = results.Count(r => r.Success);
        var failCount = results.Count(r => !r.Success);
        var totalDuration = TimeSpan.Zero; // Could track timing if needed

        _logger.LogInformation(
            "Notification sent to {SuccessCount}/{TotalCount} users successfully. Failed: {FailCount}",
            successCount,
            results.Length,
            failCount);

        // If any failures, log them (but don't fail the entire operation)
        if (failCount > 0)
        {
            var failedUsers = results.Where(r => !r.Success).Select(r => r.UserId);
            _logger.LogWarning(
                "Failed to send notification to users: {FailedUsers}",
                string.Join(", ", failedUsers));
        }

        // Only throw if ALL sends failed (catastrophic failure)
        if (failCount == results.Length)
        {
            throw new Exception($"Failed to send notification to all {results.Length} users");
        }
    }

    /// <summary>
    /// Send notification to a single user with retry handling
    /// Note: Retry is handled at the batch level, not per-user
    /// </summary>
    private async Task SendNotificationToUserAsync(Guid userId, Guid notificationId)
    {
        var userIdString = userId.ToString();

        _logger.LogDebug(
            "Sending notification via SignalR: UserId={UserId}, NotificationId={NotificationId}",
            userIdString,
            notificationId);

        // Send to user by their user ID (SignalR maps this to NameIdentifier claim)
        await _hubContext.Clients
            .User(userIdString)
            .SendAsync("ReceiveNotification", notificationId);

        _logger.LogDebug(
            "Successfully sent notification to user: UserId={UserId}, NotificationId={NotificationId}",
            userIdString,
            notificationId);
    }
}

/// <summary>
/// Result type for parallel notification sending
/// </summary>
internal record NotificationSendResult(
    bool Success,
    Guid UserId,
    Exception Error
);
