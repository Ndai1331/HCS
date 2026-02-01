using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.JSInterop;

namespace HC.Blazor.Pages.Chat1.Handlers
{
    /// <summary>
    /// Handles message-related operations for the chat system
    /// Responsible for sending, receiving, and processing messages
    /// </summary>
    public interface IChatMessageHandler
    {
        Task SendMessageAsync(string messageText, List<MessageFileDto> uploadedFiles, ChatMessageDto replyingTo);
        Task ReplyToMessageAsync(ChatMessageDto message);
        Task HandleSignalRMessage(ChatMessageRdto message);
        Task HandleCrossTabMessage(ChatMessageRdto message);
    }

    /// <summary>
    /// Implementation of chat message handler
    /// </summary>
    public class ChatMessageHandler : IChatMessageHandler, IAsyncDisposable
    {
        private readonly IConversationAppService _conversationAppService;
        private readonly ILogger<ChatMessageHandler> _logger;
        private readonly IJSRuntime _jsRuntime;
        private readonly ChatState _state;
        
        private readonly SemaphoreSlim _sendLock = new SemaphoreSlim(1, 1);
        private bool _isSendingMessage = false;
        private int _pendingMessagesCount = 0;

        public ChatMessageHandler(
            IConversationAppService conversationAppService,
            ILogger<ChatMessageHandler> logger,
            IJSRuntime jsRuntime,
            ChatState state)
        {
            _conversationAppService = conversationAppService;
            _logger = logger;
            _jsRuntime = jsRuntime;
            _state = state;
        }

        /// <summary>
        /// Send a message with thread-safety protection
        /// </summary>
        public async Task SendMessageAsync(string messageText, List<MessageFileDto> uploadedFiles, ChatMessageDto replyingTo)
        {
            // Try to acquire lock with timeout to prevent race conditions
            if (!await _sendLock.WaitAsync(TimeSpan.FromSeconds(1)))
            {
                _logger.LogWarning("Send message already in progress");
                return;
            }

            try
            {
                if (_isSendingMessage)
                {
                    _logger.LogWarning("Message send already in progress, skipping duplicate request");
                    return;
                }

                _isSendingMessage = true;

                // Validate input
                if (!ValidateMessage(messageText, uploadedFiles))
                {
                    return;
                }

                // Clear input immediately for better UX
                await ClearInputAsync();

                // Create optimistic message and add to UI immediately
                var optimisticMessage = CreateOptimisticMessage(messageText, uploadedFiles, replyingTo);
                optimisticMessage.IsSending = true;

                if (_state.CurrentConversation?.Messages == null)
                {
                    _state.CurrentConversation = new ChatConversationDto { Messages = new List<ChatMessageDto>() };
                }
                _state.CurrentConversation.Messages.Add(optimisticMessage);

                // Update UI immediately
                _state.CurrentContact.LastMessage = messageText;
                _state.CurrentContact.LastMessageDate = DateTime.UtcNow;

                // Increment pending count
                Interlocked.Increment(ref _pendingMessagesCount);

                // Update UI
                await NotifyStateChangedAsync();

                // Auto scroll to bottom to show new message
                await Task.Delay(100);
                await ScrollToBottomAsync();

                // Focus textarea for next message
                try
                {
                    await Task.Delay(50);
                    if (_state.MessageTextArea.Id != null)
                    {
                        await _state.MessageTextArea.FocusAsync();
                    }
                }
                catch
                {
                    // Ignore if element is not available
                }

                // Send to server in background (fire-and-forget pattern with proper error handling)
                _ = Task.Run(() => SendToServerAsync(messageText, uploadedFiles, replyingTo, optimisticMessage));
            }
            finally
            {
                _sendLock.Release();
            }
        }

        /// <summary>
        /// Reply to a specific message
        /// </summary>
        public async Task ReplyToMessageAsync(ChatMessageDto message)
        {
            _state.ReplyingToMessage = message;
            
            try
            {
                if (_state.MessageTextArea.Id != null)
                {
                    await _state.MessageTextArea.FocusAsync();
                }
            }
            catch
            {
                // Ignore if element is not available
            }

            await NotifyStateChangedAsync();
        }

        /// <summary>
        /// Handle incoming SignalR message
        /// </summary>
        public async Task HandleSignalRMessage(ChatMessageRdto message)
        {
            await ProcessReceivedMessage(message);
        }

        /// <summary>
        /// Handle cross-tab message
        /// </summary>
        public async Task HandleCrossTabMessage(ChatMessageRdto message)
        {
            message.IsCrossTabMessage = true;
            await ProcessReceivedMessage(message);
        }

        /// <summary>
        /// Process received message from SignalR or cross-tab
        /// </summary>
        private async Task ProcessReceivedMessage(ChatMessageRdto message)
        {
            try
            {
                var currentUser = _state.CurrentUser;
                if (currentUser == null)
                {
                    return;
                }

#if DEBUG
                _logger.LogInformation($"ChatMessageHandler: DEBUG - Message details: Id={message.Id}, SenderUserId={message.SenderUserId}, Text={message.Text}, ConversationId={message.ConversationId}");
#endif

                // Skip messages from current user in same tab (avoid duplicate)
                if (message.SenderUserId == currentUser.Id && !message.IsCrossTabMessage)
                {
                    return;
                }

                // Determine if message is for current conversation
                bool isForCurrentConversation = IsForCurrentConversation(message, currentUser);

                if (isForCurrentConversation)
                {
                    await RefreshConversationAsync();
                    await ScrollToBottomAsync();
                }
                else
                {
                    await UpdateConversationListAsync(message, currentUser);
                }

                await NotifyStateChangedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing received message");
            }
        }

        /// <summary>
        /// Check if message is for the current conversation
        /// </summary>
        private bool IsForCurrentConversation(ChatMessageRdto message, CurrentUserDto currentUser)
        {
            if (_state.CurrentContact == null)
            {
                return false;
            }

            if (_state.CurrentContact.Type == ConversationType.User)
            {
                // Check ConversationId FIRST if both message and current contact have it
                if (message.ConversationId.HasValue && _state.CurrentContact.ConversationId.HasValue)
                {
                    return message.ConversationId.Value == _state.CurrentContact.ConversationId.Value;
                }
                else
                {
                    // Fallback: For old User conversations without ConversationId, check based on sender
                    bool isFromOtherUser = _state.CurrentContact.UserId == message.SenderUserId;
                    bool isFromCurrentUser = message.SenderUserId == currentUser.Id;
                    return isFromOtherUser || isFromCurrentUser;
                }
            }
            else if (_state.CurrentContact.Type != ConversationType.User && _state.CurrentConversationId.HasValue)
            {
                // For group conversations: check if message belongs to current conversation
                return message.ConversationId.HasValue &&
                       message.ConversationId.Value == _state.CurrentConversationId.Value;
            }

            return false;
        }

        /// <summary>
        /// Send message to server with error handling
        /// </summary>
        private async Task SendToServerAsync(string messageText, List<MessageFileDto> uploadedFiles, ChatMessageDto replyingTo, ChatMessageDto optimisticMessage)
        {
            try
            {
                ChatMessageDto serverMessage = null;
                var targetUserId = _state.CurrentContact.UserId;
                var conversationId = _state.CurrentConversationId;

                if (replyingTo != null)
                {
                    // Send reply message
                    serverMessage = await _conversationAppService.SendReplyMessageAsync(new SendReplyMessageInput
                    {
                        TargetUserId = targetUserId,
                        ConversationId = conversationId,
                        ReplyToMessageId = replyingTo.Id,
                        Message = messageText ?? string.Empty
                    });
                }
                else if (uploadedFiles != null && uploadedFiles.Any())
                {
                    // Send message with files
                    serverMessage = await _conversationAppService.SendMessageWithFilesAsync(new SendMessageWithFilesInput
                    {
                        TargetUserId = targetUserId,
                        ConversationId = conversationId,
                        Message = messageText,
                        FileIds = uploadedFiles.Select(f => f.Id).ToList()
                    });
                }
                else
                {
                    // Send normal message
                    serverMessage = await _conversationAppService.SendMessageAsync(new SendMessageInput
                    {
                        Message = messageText,
                        ConversationId = conversationId ?? throw new InvalidOperationException("ConversationId is required")
                    });
                }

                // Update optimistic message with server response
                await UpdateMessageAfterSendAsync(serverMessage, optimisticMessage);
            }
            catch (Exception ex)
            {
                // Handle error on UI thread
                await HandleSendErrorAsync(ex, optimisticMessage);
            }
            finally
            {
                // Reset sending flag
                _isSendingMessage = false;
                Interlocked.Decrement(ref _pendingMessagesCount);
            }
        }

        /// <summary>
        /// Update message after successful send
        /// </summary>
        private async Task UpdateMessageAfterSendAsync(ChatMessageDto serverMessage, ChatMessageDto optimisticMessage)
        {
            if (serverMessage == null || _state.CurrentConversation?.Messages == null)
            {
                return;
            }

            // Mark server message as sent
            serverMessage.IsSending = false;

            // Replace optimistic message with server message
            var index = _state.CurrentConversation.Messages.FindIndex(m => m.Id == optimisticMessage.Id);
            if (index >= 0)
            {
                _state.CurrentConversation.Messages[index] = serverMessage;
            }
            else
            {
                _state.CurrentConversation.Messages.Add(serverMessage);
            }

            // Update last message from server
            var lastMessage = _state.CurrentConversation.Messages.LastOrDefault();
            if (lastMessage != null)
            {
                _state.CurrentContact.LastMessage = lastMessage.Message;
                _state.CurrentContact.LastMessageDate = lastMessage.MessageDate;
            }

            await NotifyStateChangedAsync();

            // Auto scroll to bottom after server message is updated
            await Task.Delay(100);
            await ScrollToBottomAsync();
        }

        /// <summary>
        /// Handle send error
        /// </summary>
        private async Task HandleSendErrorAsync(Exception ex, ChatMessageDto optimisticMessage)
        {
            _logger.LogError(ex, "Error sending message");

            // Remove optimistic message on error
            if (_state.CurrentConversation?.Messages != null)
            {
                _state.CurrentConversation.Messages.RemoveAll(m => m.Id == optimisticMessage.Id);
            }

            await NotifyStateChangedAsync();
            
            // Show error to user (implement this based on your UI)
            // await _uiMessageService.Error("Failed to send message");
        }

        /// <summary>
        /// Validate message before sending
        /// </summary>
        private bool ValidateMessage(string messageText, List<MessageFileDto> uploadedFiles)
        {
            if (_isSendingMessage)
                return false;

            if (string.IsNullOrWhiteSpace(messageText) && (uploadedFiles == null || !uploadedFiles.Any()))
                return false;

            if (_state.CurrentContact == null)
                return false;

            return true;
        }

        /// <summary>
        /// Clear input fields
        /// </summary>
        private async Task ClearInputAsync()
        {
            // Clear textarea via JavaScript FIRST to ensure immediate clearing
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval",
                    "const textarea = document.querySelector('textarea.form-control'); " +
                    "if (textarea) { " +
                    "  textarea.value = ''; " +
                    "  textarea.dispatchEvent(new Event('input', { bubbles: true })); " +
                    "}");
            }
            catch
            {
                // Ignore errors
            }

            _state.MessageText = string.Empty;
            _state.ReplyingToMessage = null;
            _state.UploadedFiles?.Clear();
        }

        /// <summary>
        /// Create optimistic message for UI update
        /// </summary>
        private ChatMessageDto CreateOptimisticMessage(string messageText, List<MessageFileDto> files, ChatMessageDto replyingTo)
        {
            var currentUserId = _state.CurrentUser?.Id ?? Guid.Empty;
            var now = DateTime.UtcNow;

            return new ChatMessageDto
            {
                Id = Guid.NewGuid(), // Temporary ID
                Message = messageText,
                MessageDate = now,
                Side = ChatMessageSide.Sender,
                IsRead = false,
                ReadDate = default(DateTime),
                ReplyToMessageId = replyingTo?.Id,
                ReplyToMessage = replyingTo != null ? new ChatMessageDto
                {
                    Id = replyingTo.Id,
                    Message = replyingTo.Message,
                    MessageDate = replyingTo.MessageDate,
                    Side = replyingTo.Side
                } : null,
                Files = files?.Select(f => new MessageFileDto
                {
                    Id = f.Id,
                    MessageId = f.MessageId,
                    FileName = f.FileName,
                    ContentType = f.ContentType,
                    FileSize = f.FileSize,
                    FileExtension = f.FileExtension,
                    DownloadUrl = f.DownloadUrl,
                    CreationTime = f.CreationTime
                }).ToList() ?? new List<MessageFileDto>(),
                SenderUserId = currentUserId,
                SenderName = _state.CurrentUser?.Name,
                SenderSurname = _state.CurrentUser?.SurName,
                SenderUsername = _state.CurrentUser?.UserName
            };
        }

        /// <summary>
        /// Refresh conversation when receiving new message
        /// </summary>
        private async Task RefreshConversationAsync()
        {
            if (_state.CurrentContact == null)
            {
                return;
            }

            if (_state.CurrentContact.Type == ConversationType.User)
            {
                _state.CurrentConversation = await _conversationAppService.GetConversationAsync(
                    new GetConversationInput { TargetUserId = _state.CurrentContact.UserId, MaxResultCount = 100 });
            }
            else if (_state.CurrentContact.ConversationId.HasValue)
            {
                _state.CurrentConversation = await _conversationAppService.GetConversationAsync(
                    new GetConversationInput { ConversationId = _state.CurrentContact.ConversationId.Value, TargetUserId = Guid.Empty, MaxResultCount = 100 });
            }

            if (_state.CurrentConversation != null)
            {
                _state.CurrentConversation.Messages.Reverse();
                var lastMessage = _state.CurrentConversation.Messages.LastOrDefault();
                _state.CurrentContact.LastMessage = lastMessage?.Message;
                _state.CurrentContact.LastMessageDate = lastMessage?.MessageDate;
            }
        }

        /// <summary>
        /// Update conversation list with new message info
        /// </summary>
        private async Task UpdateConversationListAsync(ChatMessageRdto message, CurrentUserDto currentUser)
        {
            // Find the conversation in the list and update unread count + last message
            ChatContactDto targetContact = null;

            if (message.ConversationId.HasValue)
            {
                targetContact = _state.ContactList?.FirstOrDefault(c =>
                    c.ConversationId.HasValue && c.ConversationId.Value == message.ConversationId.Value);
            }

            if (targetContact == null && message.SenderUserId != currentUser.Id)
            {
                targetContact = _state.ContactList?.FirstOrDefault(c =>
                    c.Type == ConversationType.User && c.UserId == message.SenderUserId);
            }

            if (targetContact != null && _state.ContactList != null)
            {
                // Update unread count (only if message is from someone else)
                if (message.SenderUserId != currentUser.Id)
                {
                    targetContact.UnreadMessageCount++;
                }

                // Update last message info
                targetContact.LastMessage = message.Text;
                targetContact.LastMessageDate = DateTime.Now;

                // Move conversation to top of list for better UX
                _state.ContactList.Remove(targetContact);
                _state.ContactList.Insert(0, targetContact);
            }
        }

        /// <summary>
        /// Scroll chat to bottom
        /// </summary>
        private async Task ScrollToBottomAsync()
        {
            try
            {
                await _jsRuntime.InvokeVoidAsync("eval",
                    "const container = document.getElementById('chat_conversation_wrapper'); " +
                    "if (container) { " +
                    "  container.scrollTop = container.scrollHeight; " +
                    "}");
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Notify that state has changed
        /// </summary>
        private async Task NotifyStateChangedAsync()
        {
            if (_state.OnChange != null)
            {
                await _state.OnChange.Invoke();
            }
        }

        public async ValueTask DisposeAsync()
        {
            await Task.Run(() =>
            {
                _sendLock?.Dispose();
            });
        }
    }
}
