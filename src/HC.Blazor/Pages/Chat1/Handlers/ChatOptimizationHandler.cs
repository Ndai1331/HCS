using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Pages.Chat1.Handlers
{
    /// <summary>
    /// Optimized conversation handler that appends new messages instead of full refresh
    /// </summary>
    public interface IChatOptimizationHandler
    {
        Task AppendMessageAsync(ChatMessageRdto messageData);
        Task<bool> ShouldRefreshConversationAsync(ChatMessageRdto messageData);
        Task MarkMessageAsReadAsync(Guid messageId);
    }

    /// <summary>
    /// Implementation with message-level updates for better performance
    /// </summary>
    public class ChatOptimizationHandler : IChatOptimizationHandler
    {
        private readonly ILogger<ChatOptimizationHandler> _logger;
        private readonly ChatState _state;

        // Cache for message deduplication
        private readonly HashSet<Guid> _processedMessageIds = new HashSet<Guid>();
        private readonly SemaphoreSlim _lock = new SemaphoreSlim(1, 1);

        public ChatOptimizationHandler(
            ILogger<ChatOptimizationHandler> logger,
            ChatState state)
        {
            _logger = logger;
            _state = state;
        }

        /// <summary>
        /// Append a new message to the conversation without full refresh
        /// </summary>
        public async Task AppendMessageAsync(ChatMessageRdto messageData)
        {
            await _lock.WaitAsync();
            try
            {
                // Check if message already processed (deduplication)
                if (_processedMessageIds.Contains(messageData.Id))
                {
                    _logger.LogDebug("Message {MessageId} already processed, skipping", messageData.Id);
                    return;
                }

                // Mark as processed
                _processedMessageIds.Add(messageData.Id);

                // Limit cache size (keep last 200 messages)
                if (_processedMessageIds.Count > 200)
                {
                    var oldest = _processedMessageIds.First();
                    _processedMessageIds.Remove(oldest);
                }

                // Convert and append message
                var message = ConvertToMessageDto(messageData);

                if (_state.CurrentConversation?.Messages != null)
                {
                    // Check if message already exists (double-check)
                    if (_state.CurrentConversation.Messages.Any(m => m.Id == message.Id))
                    {
                        return;
                    }

                    // Append message
                    _state.CurrentConversation.Messages.Add(message);

                    // Update last message info
                    var lastMessage = _state.CurrentConversation.Messages.LastOrDefault();
                    if (lastMessage != null && _state.CurrentContact != null)
                    {
                        _state.CurrentContact.LastMessage = lastMessage.Message;
                        _state.CurrentContact.LastMessageDate = lastMessage.MessageDate;
                    }

                    _logger.LogDebug(
                        "Appended message {MessageId} to conversation. Total messages: {Count}",
                        message.Id,
                        _state.CurrentConversation.Messages.Count);
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Determine if a full conversation refresh is needed
        /// </summary>
        public async Task<bool> ShouldRefreshConversationAsync(ChatMessageRdto messageData)
        {
            // Full refresh needed if:
            // 1. Message conversation ID doesn't match current
            // 2. Current conversation is null
            // 3. First message in conversation

            if (_state.CurrentConversation == null)
            {
                return true;
            }

            if (_state.CurrentConversationId == null)
            {
                return true;
            }

            if (!messageData.ConversationId.HasValue)
            {
                return false; // Can't determine, assume append
            }

            // If conversation ID matches, we can append
            if (messageData.ConversationId.Value == _state.CurrentConversationId.Value)
            {
                return false; // Can append, no full refresh needed
            }

            // Different conversation - check if it's the current one
            return messageData.ConversationId.Value != _state.CurrentConversationId.Value;
        }

        /// <summary>
        /// Mark a message as read
        /// </summary>
        public async Task MarkMessageAsReadAsync(Guid messageId)
        {
            await _lock.WaitAsync();
            try
            {
                if (_state.CurrentConversation?.Messages != null)
                {
                    var message = _state.CurrentConversation.Messages.FirstOrDefault(m => m.Id == messageId);
                    if (message != null)
                    {
                        message.IsRead = true;
                        message.ReadDate = DateTime.UtcNow;
                        _logger.LogDebug("Marked message {MessageId} as read", messageId);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
        }

        /// <summary>
        /// Convert ChatMessageRdto to ChatMessageDto
        /// </summary>
        private ChatMessageDto ConvertToMessageDto(ChatMessageRdto messageData)
        {
            return new ChatMessageDto
            {
                Id = messageData.Id,
                ConversationId = messageData.ConversationId,
                Message = messageData.Text,
                MessageDate = DateTime.UtcNow,
                Side = messageData.SenderUserId == _state.CurrentUser?.Id 
                    ? ChatMessageSide.Sender 
                    : ChatMessageSide.Receiver,
                IsRead = false,
                SenderUserId = messageData.SenderUserId,
                SenderUsername = messageData.SenderUsername,
                SenderName = messageData.SenderName,
                SenderSurname = messageData.SenderSurname
            };
        }

        /// <summary>
        /// Clear processed message cache (useful when switching conversations)
        /// </summary>
        public async Task ClearCacheAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _processedMessageIds.Clear();
                _logger.LogDebug("Cleared message cache");
            }
            finally
            {
                _lock.Release();
            }
        }
    }
}
