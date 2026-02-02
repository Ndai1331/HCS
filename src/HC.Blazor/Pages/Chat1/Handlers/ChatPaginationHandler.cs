using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Microsoft.Extensions.Logging;
using HC.Chat.Messages;

namespace HC.Blazor.Pages.Chat1.Handlers
{
    /// <summary>
    /// Handles pagination for messages and conversations
    /// </summary>
    public interface IChatPaginationHandler
    {
        Task LoadMoreMessagesAsync();
        Task LoadMoreConversationsAsync();
        Task HandleScrollAsync();
        void ResetPagination();
    }

    /// <summary>
    /// Implementation of chat pagination handler
    /// </summary>
    public class ChatPaginationHandler : IChatPaginationHandler
    {
        private readonly IConversationAppService _conversationAppService;
        private readonly ILogger<ChatPaginationHandler> _logger;
        private readonly IJSRuntime _jsRuntime;
        private readonly ChatState _state;
        private readonly PaginationState _pagination;

        private const int MessagesPageSize = 10;
        private const int ConversationsPageSize = 10;

        public ChatPaginationHandler(
            IConversationAppService conversationAppService,
            ILogger<ChatPaginationHandler> logger,
            IJSRuntime jsRuntime,
            ChatState state,
            PaginationState pagination)
        {
            _conversationAppService = conversationAppService;
            _logger = logger;
            _jsRuntime = jsRuntime;
            _state = state;
            _pagination = pagination;
        }

        /// <summary>
        /// Load more messages when scrolling to top
        /// </summary>
        public async Task LoadMoreMessagesAsync()
        {
            if (_pagination.IsLoadingMoreMessages || 
                !_pagination.HasMoreMessages || 
                _state.CurrentContact == null)
            {
                return;
            }

            _pagination.IsLoadingMoreMessages = true;
            await _state.NotifyStateChangedAsync();

            try
            {
                List<ChatMessageDto> newMessages;

                if (_state.CurrentContact.Type == ConversationType.User)
                {
                    var conversation = await _conversationAppService.GetConversationAsync(new GetConversationInput
                    {
                        TargetUserId = _state.CurrentContact.UserId,
                        SkipCount = _pagination.MessageSkipCount,
                        MaxResultCount = MessagesPageSize
                    });
                    newMessages = conversation?.Messages ?? new List<ChatMessageDto>();
                }
                else if (_state.CurrentConversationId.HasValue)
                {
                    var conversation = await _conversationAppService.GetConversationAsync(new GetConversationInput
                    {
                        ConversationId = _state.CurrentConversationId.Value,
                        TargetUserId = Guid.Empty,
                        SkipCount = _pagination.MessageSkipCount,
                        MaxResultCount = MessagesPageSize
                    });
                    newMessages = conversation?.Messages ?? new List<ChatMessageDto>();
                }
                else
                {
                    return;
                }

                if (newMessages.Any())
                {
                    // Reverse to maintain chronological order (oldest first)
                    newMessages.Reverse();

                    // Insert at beginning
                    _state.CurrentConversation.Messages.InsertRange(0, newMessages);

                    _pagination.MessageSkipCount += newMessages.Count;

                    // Check if there are more messages
                    if (newMessages.Count < MessagesPageSize)
                    {
                        _pagination.HasMoreMessages = false;
                    }

                    // Maintain scroll position
                    await MaintainScrollPositionAsync();
                }
                else
                {
                    _pagination.HasMoreMessages = false;
                }

                await _state.NotifyStateChangedAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more messages");
            }
            finally
            {
                _pagination.IsLoadingMoreMessages = false;
                await _state.NotifyStateChangedAsync();
            }
        }

        /// <summary>
        /// Load more conversations when scrolling to bottom
        /// </summary>
        public async Task LoadMoreConversationsAsync()
        {
            if (_pagination.IsLoadingMoreConversations || !_pagination.HasMoreConversations)
            {
                return;
            }

            _pagination.IsLoadingMoreConversations = true;
            await _state.NotifyStateChangedAsync();

            try
            {
                // This will be implemented by calling the main contact loading service
                // For now, just update state
                await Task.Delay(100);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading more conversations");
            }
            finally
            {
                _pagination.IsLoadingMoreConversations = false;
                await _state.NotifyStateChangedAsync();
            }
        }

        /// <summary>
        /// Handle scroll events to trigger loading
        /// </summary>
        public async Task HandleScrollAsync()
        {
            if (_pagination.IsLoadingMoreMessages || 
                !_pagination.HasMoreMessages || 
                _state.CurrentConversation?.Messages == null)
            {
                return;
            }

            try
            {
                // Check if scrolled to top (within 100px)
                var scrollTop = await _jsRuntime.InvokeAsync<double>("eval",
                    "document.getElementById('chat_conversation_wrapper')?.scrollTop || 0");

                if (scrollTop <= 100) // Near top, load more messages
                {
                    await LoadMoreMessagesAsync();
                }
            }
            catch
            {
                // Ignore errors
            }
        }

        /// <summary>
        /// Reset pagination state
        /// </summary>
        public void ResetPagination()
        {
            _pagination.ResetMessages();
            _pagination.ResetConversations();
        }

        /// <summary>
        /// Maintain scroll position after loading more messages
        /// </summary>
        private async Task MaintainScrollPositionAsync()
        {
            try
            {
                await Task.Delay(50); // Wait for DOM update
                await _jsRuntime.InvokeVoidAsync("eval",
                    "const container = document.getElementById('chat_conversation_wrapper'); " +
                    "if (container) { " +
                    "  const oldScroll = container.scrollHeight; " +
                    "  setTimeout(() => { " +
                    "    const newScroll = container.scrollHeight; " +
                    "    container.scrollTop = newScroll - oldScroll; " +
                    "  }, 10); " +
                    "}");
            }
            catch
            {
                // Ignore errors
            }
        }
    }
}
