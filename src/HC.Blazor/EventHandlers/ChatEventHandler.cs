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

namespace HC.Blazor.EventHandlers;

/// <summary>
/// Handles chat message events and pushes messages via SignalR
/// </summary>
public class ChatEventHandler :
    IDistributedEventHandler<ChatMessageEto>,
    IDistributedEventHandler<ChatDeletedMessageEto>,
    IDistributedEventHandler<ChatDeletedConversationEto>,
    IDistributedEventHandler<ConversationCreatedEto>,
    ITransientDependency
{
    private readonly IHubContext<ChatHub> _hubContext;
    private readonly ILogger<ChatEventHandler> _logger;

    public ChatEventHandler(
        IHubContext<ChatHub> hubContext,
        ILogger<ChatEventHandler> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task HandleEventAsync(ChatMessageEto eventData)
    {
        try
        {
            Console.WriteLine($"ChatEventHandler: Handling ChatMessageEto - MessageId: {eventData.MessageId}, SenderUserId: {eventData.SenderUserId}, TargetUserId: {eventData.TargetUserId}, ConversationId: {eventData.ConversationId}, Message: {eventData.Message}");

            _logger.LogInformation(
                "Handling ChatMessageEto: MessageId={MessageId}, SenderUserId={SenderUserId}, TargetUserId={TargetUserId}",
                eventData.MessageId,
                eventData.SenderUserId,
                eventData.TargetUserId);

            var targetUserIdString = eventData.TargetUserId.ToString();

            // Send message to target user via SignalR
            _logger.LogInformation(
                "Attempting to send chat message via SignalR: TargetUserId={TargetUserId}, MessageId={MessageId}",
                targetUserIdString,
                eventData.MessageId);

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

            Console.WriteLine($"ChatEventHandler: Sending message data to SignalR - TargetUser: {targetUserIdString}, MessageData: {System.Text.Json.JsonSerializer.Serialize(messageData)}");

            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("ReceiveMessage", messageData);

            Console.WriteLine($"ChatEventHandler: Successfully sent message via SignalR");

            _logger.LogInformation(
                "Successfully sent chat message to user: TargetUserId={TargetUserId}, MessageId={MessageId}",
                targetUserIdString,
                eventData.MessageId);
            
            // Also send ChatUnreadCountChanged event to update the badge on notification bar
            try
            {
                await _hubContext.Clients
                    .User(targetUserIdString)
                    .SendAsync("ChatUnreadCountChanged");
                
                _logger.LogInformation(
                    "Successfully sent ChatUnreadCountChanged event: TargetUserId={TargetUserId}",
                    targetUserIdString);
            }
            catch (Exception ex2)
            {
                _logger.LogError(ex2, "Error sending ChatUnreadCountChanged event");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ChatEventHandler: Error handling ChatMessageEto: {ex.Message}");
            _logger.LogError(ex,
                "Error handling ChatMessageEto: MessageId={MessageId}, TargetUserId={TargetUserId}",
                eventData.MessageId,
                eventData.TargetUserId);
        }
    }

    public async Task HandleEventAsync(ChatDeletedMessageEto eventData)
    {
        try
        {
            _logger.LogInformation(
                "Handling ChatDeletedMessageEto: MessageId={MessageId}, TargetUserId={TargetUserId}",
                eventData.MessageId,
                eventData.TargetUserId);

            var targetUserIdString = eventData.TargetUserId.ToString();

            // Send delete message notification to target user via SignalR
            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("MessageDeleted", eventData.MessageId);

            _logger.LogInformation(
                "Successfully sent delete message notification: TargetUserId={TargetUserId}, MessageId={MessageId}",
                targetUserIdString,
                eventData.MessageId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling ChatDeletedMessageEto: MessageId={MessageId}, TargetUserId={TargetUserId}",
                eventData.MessageId,
                eventData.TargetUserId);
        }
    }

    public async Task HandleEventAsync(ChatDeletedConversationEto eventData)
    {
        try
        {
            _logger.LogInformation(
                "Handling ChatDeletedConversationEto: UserId={UserId}, TargetUserId={TargetUserId}",
                eventData.UserId,
                eventData.TargetUserId);

            var targetUserIdString = eventData.TargetUserId.ToString();

            // Send delete conversation notification to target user via SignalR
            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("ConversationDeleted", eventData.UserId);

            _logger.LogInformation(
                "Successfully sent delete conversation notification: TargetUserId={TargetUserId}, UserId={UserId}",
                targetUserIdString,
                eventData.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling ChatDeletedConversationEto: UserId={UserId}, TargetUserId={TargetUserId}",
                eventData.UserId,
                eventData.TargetUserId);
        }
    }

    public async Task HandleEventAsync(ConversationCreatedEto eventData)
    {
        try
        {
            _logger.LogInformation(
                "Handling ConversationCreatedEto: ConversationId={ConversationId}, TargetUserId={TargetUserId}, Type={Type}, CreatorUserId={CreatorUserId}",
                eventData.ConversationId,
                eventData.TargetUserId,
                eventData.Type,
                eventData.CreatorUserId);

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

            // Send new conversation notification to target user via SignalR
            await _hubContext.Clients
                .User(targetUserIdString)
                .SendAsync("ConversationCreated", conversationData);

            _logger.LogInformation(
                "Successfully sent new conversation notification: TargetUserId={TargetUserId}, ConversationId={ConversationId}",
                targetUserIdString,
                eventData.ConversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error handling ConversationCreatedEto: ConversationId={ConversationId}, TargetUserId={TargetUserId}",
                eventData.ConversationId,
                eventData.TargetUserId);
        }
    }
}