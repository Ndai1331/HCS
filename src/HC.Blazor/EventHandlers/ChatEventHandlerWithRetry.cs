using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Volo.Abp.DependencyInjection;
using Volo.Abp.EventBus.Distributed;
using HC.Blazor.Hubs;
using HC.Blazor.Services;

namespace HC.Blazor.EventHandlers;

/// <summary>
/// Enhanced chat event handler with retry policies, circuit breaker, and dead letter queue
/// </summary>
public class ChatEventHandlerWithRetry :
    IDistributedEventHandler<ChatMessageEto>,
    IDistributedEventHandler<ChatDeletedMessageEto>,
    IDistributedEventHandler<ChatDeletedConversationEto>,
    IDistributedEventHandler<ConversationCreatedEto>,
    ITransientDependency
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatEventHandlerWithRetry> _logger;
    private readonly RetryPolicy _retryPolicy;
    private readonly CircuitBreaker _circuitBreaker;
    private readonly IDeadLetterQueue _deadLetterQueue;

    public ChatEventHandlerWithRetry(
        IHubContext<ChatHub> hubContext,
        ILogger<ChatEventHandlerWithRetry> logger,
        IDeadLetterQueue deadLetterQueue)
    {
        _hubContext = hubContext;
        _logger = logger;
        _deadLetterQueue = deadLetterQueue;
        
        // Initialize retry policy: 3 retries, exponential backoff starting at 1s, max 30s
        _retryPolicy = new RetryPolicy(
            logger,
            maxRetries: 3,
            initialDelay: TimeSpan.FromSeconds(1),
            maxDelay: TimeSpan.FromSeconds(30),
            backoffMultiplier: 2.0);
        
        // Initialize circuit breaker: opens after 5 failures, resets after 1 minute
        _circuitBreaker = new CircuitBreaker(
            logger,
            failureThreshold: 5,
            openTimeout: TimeSpan.FromMinutes(1));
    }

        public async Task HandleEventAsync(ChatMessageEto eventData)
        {
            try
            {
                _logger.LogInformation(
                    "Handling ChatMessageEto: MessageId={MessageId}, SenderUserId={SenderUserId}, TargetUserId={TargetUserId}",
                    eventData.MessageId,
                    eventData.SenderUserId,
                    eventData.TargetUserId);

                // Execute with circuit breaker and retry policy
                    await _circuitBreaker.ExecuteAsync(
                    async () => await _retryPolicy.ExecuteAsync(
                        () => SendMessageAsync(eventData),
                        "SendMessage"),
                    "ChatMessage");

                _logger.LogInformation(
                    "Successfully sent chat message: MessageId={MessageId}, TargetUserId={TargetUserId}",
                    eventData.MessageId,
                    eventData.TargetUserId);
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

        private async Task SendMessageAsync(ChatMessageEto eventData)
        {
            var targetUserIdString = eventData.TargetUserId.ToString();

            // Create message data to send to client
            var messageData = new
            {
                Id = eventData.MessageId,
                ConversationId = eventData.ConversationId,
                SenderUserId = eventData.SenderUserId,
                SenderUsername = eventData.SenderUserName,
                SenderName = eventData.SenderName,
                SenderSurname = eventData.SenderSurname,
                Text = eventData.Message,
                MessageDate = DateTime.UtcNow
            };

            _logger.LogDebug(
                "Sending message data to SignalR - TargetUser: {TargetUserId}, MessageData: {MessageData}",
                targetUserIdString,
                System.Text.Json.JsonSerializer.Serialize(messageData));

            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("ReceiveMessage", messageData);

            _logger.LogDebug(
                "Message sent successfully to user: {TargetUserId}",
                targetUserIdString);
        }

        public async Task HandleEventAsync(ChatDeletedMessageEto eventData)
        {
            try
            {
                _logger.LogInformation(
                    "Handling ChatDeletedMessageEto: MessageId={MessageId}, TargetUserId={TargetUserId}",
                    eventData.MessageId,
                    eventData.TargetUserId);

                await _circuitBreaker.ExecuteAsync(
                    async () => await _retryPolicy.ExecuteAsync(
                        () => SendDeleteMessageAsync(eventData),
                        "DeleteMessage"),
                    "DeleteMessage");

                _logger.LogInformation(
                    "Successfully sent delete message notification: MessageId={MessageId}",
                    eventData.MessageId);
            }
            catch (CircuitBreakerOpenException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for delete message");
                await _deadLetterQueue.AddAsync(eventData, "Circuit breaker open", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delete message after retries");
                await _deadLetterQueue.AddAsync(eventData, "Max retries exceeded", ex);
            }
        }

    private async Task SendDeleteMessageAsync(ChatDeletedMessageEto eventData)
    {
        var targetUserIdString = eventData.TargetUserId.ToString();

        await _hubContext.Clients
            .User(targetUserIdString)
            .SendAsync("MessageDeleted", eventData.MessageId);
    }

        public async Task HandleEventAsync(ChatDeletedConversationEto eventData)
        {
            try
            {
                _logger.LogInformation(
                    "Handling ChatDeletedConversationEto: UserId={UserId}, TargetUserId={TargetUserId}",
                    eventData.UserId,
                    eventData.TargetUserId);

                await _circuitBreaker.ExecuteAsync(
                    async () => await _retryPolicy.ExecuteAsync(
                        () => SendDeleteConversationAsync(eventData),
                        "DeleteConversation"),
                    "DeleteConversation");

                _logger.LogInformation(
                    "Successfully sent delete conversation notification: UserId={UserId}",
                    eventData.UserId);
            }
            catch (CircuitBreakerOpenException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for delete conversation");
                await _deadLetterQueue.AddAsync(eventData, "Circuit breaker open", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send delete conversation after retries");
                await _deadLetterQueue.AddAsync(eventData, "Max retries exceeded", ex);
            }
        }

    private async Task SendDeleteConversationAsync(ChatDeletedConversationEto eventData)
    {
        var targetUserIdString = eventData.TargetUserId.ToString();

        await _hubContext.Clients
            .User(targetUserIdString)
            .SendAsync("ConversationDeleted", eventData.UserId);
    }

        public async Task HandleEventAsync(ConversationCreatedEto eventData)
        {
            try
            {
                _logger.LogInformation(
                    "Handling ConversationCreatedEto: ConversationId={ConversationId}, TargetUserId={TargetUserId}",
                    eventData.ConversationId,
                    eventData.TargetUserId);

                await _circuitBreaker.ExecuteAsync(
                    async () => await _retryPolicy.ExecuteAsync(
                        () => SendConversationCreatedAsync(eventData),
                        "CreateConversation"),
                    "CreateConversation");

                _logger.LogInformation(
                    "Successfully sent new conversation notification: ConversationId={ConversationId}",
                    eventData.ConversationId);
            }
            catch (CircuitBreakerOpenException ex)
            {
                _logger.LogError(ex, "Circuit breaker is open for create conversation");
                await _deadLetterQueue.AddAsync(eventData, "Circuit breaker open", ex);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send conversation created after retries");
                await _deadLetterQueue.AddAsync(eventData, "Max retries exceeded", ex);
            }
        }

    private async Task SendConversationCreatedAsync(ConversationCreatedEto eventData)
    {
        var targetUserIdString = eventData.TargetUserId.ToString();

        // Create conversation data to send to client
        var conversationData = new
        {
            ConversationId = eventData.ConversationId,
            Type = eventData.Type.ToString(),
            ConversationName = eventData.ConversationName,
            CreatorUserId = eventData.CreatorUserId,
            CreatorUserName = eventData.CreatorUserName,
            CreatorName = eventData.CreatorName,
            CreatorSurname = eventData.CreatorSurname,
            CreatedDate = DateTime.UtcNow
        };

        await _hubContext.Clients
            .User(targetUserIdString)
            .SendAsync("ConversationCreated", conversationData);
    }
}
