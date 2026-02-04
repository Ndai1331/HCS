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
using System.Threading;

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
    /// Send notifications to all users in parallel with throttling and batching
    /// Optimized to prevent resource spikes when sending to large user counts
    /// </summary>
    private async Task SendNotificationsInParallelAsync(NotificationCreatedEto eventData)
    {
        const int MaxConcurrency = 50; // Limit concurrent sends to prevent resource spikes
        const int BatchSize = 100; // Process in batches for very large user counts
        
        var userIds = eventData.ReceiverUserIds.ToList();
        var totalUsers = userIds.Count;
        
        // If user count is small, use throttled parallel approach
        if (totalUsers <= BatchSize)
        {
            _logger.LogDebug(
                "Processing small batch of {UserCount} users with throttling (max concurrency: {MaxConcurrency})",
                totalUsers,
                MaxConcurrency);
            
            var (success, failed) = await SendNotificationsWithThrottlingAsync(
                userIds, 
                eventData.NotificationId, 
                MaxConcurrency);
            
            LogNotificationResults(success, failed, totalUsers, eventData.NotificationId);
            
            // Only throw if ALL sends failed (catastrophic failure)
            if (failed == totalUsers)
            {
                throw new Exception($"Failed to send notification to all {totalUsers} users");
            }
            
            return;
        }
        
        // For large user counts, process in batches
        _logger.LogInformation(
            "Processing large notification in batches: TotalUsers={TotalUsers}, BatchSize={BatchSize}",
            totalUsers,
            BatchSize);
        
        var totalSuccess = 0;
        var totalFailed = 0;
        var totalBatches = (totalUsers + BatchSize - 1) / BatchSize;
        
        for (int i = 0; i < totalUsers; i += BatchSize)
        {
            var batchNumber = (i / BatchSize) + 1;
            var batch = userIds.Skip(i).Take(BatchSize).ToList();
            
            _logger.LogInformation(
                "Processing batch {BatchNumber}/{TotalBatches} with {UserCount} users",
                batchNumber,
                totalBatches,
                batch.Count);
            
            var (success, failed) = await SendNotificationsWithThrottlingAsync(
                batch, 
                eventData.NotificationId, 
                MaxConcurrency);
            
            totalSuccess += success;
            totalFailed += failed;
            
            // Small delay between batches to prevent overwhelming the system
            if (i + BatchSize < totalUsers)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100));
            }
        }
        
        LogNotificationResults(totalSuccess, totalFailed, totalUsers, eventData.NotificationId);
        
        // Only throw if ALL sends failed (catastrophic failure)
        if (totalFailed == totalUsers)
        {
            throw new Exception($"Failed to send notification to all {totalUsers} users");
        }
    }
    
    /// <summary>
    /// Send notifications with throttling using SemaphoreSlim
    /// </summary>
    private async Task<(int Success, int Failed)> SendNotificationsWithThrottlingAsync(
        List<Guid> userIds, 
        Guid notificationId, 
        int maxConcurrency)
    {
        using var semaphore = new SemaphoreSlim(maxConcurrency);
        var results = await Task.WhenAll(userIds.Select(async userId =>
        {
            await semaphore.WaitAsync();
            try
            {
                await SendNotificationToUserAsync(userId, notificationId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }));
        
        var success = results.Count(r => r);
        var failed = results.Count(r => !r);
        
        return (success, failed);
    }
    
    /// <summary>
    /// Log notification sending results
    /// </summary>
    private void LogNotificationResults(int successCount, int failCount, int totalCount, Guid notificationId)
    {
        if (failCount == 0)
        {
            _logger.LogInformation(
                "Notification {NotificationId} sent to all {SuccessCount}/{TotalCount} users successfully",
                notificationId,
                successCount,
                totalCount);
        }
        else if (successCount == 0)
        {
            _logger.LogError(
                "Notification {NotificationId} failed to send to all {TotalCount} users",
                notificationId,
                totalCount);
        }
        else
        {
            _logger.LogWarning(
                "Notification {NotificationId} partial success: {SuccessCount}/{TotalCount} sent, {FailCount} failed",
                notificationId,
                successCount,
                totalCount,
                failCount);
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
