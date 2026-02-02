using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;
using Microsoft.AspNetCore.Components;

namespace HC.Blazor.Pages.Chat1
{
    /// <summary>
    /// Centralized state management for Chat1 component
    /// Provides a single source of truth for all chat-related data
    /// </summary>
    public class ChatState
    {
        // Core conversation data
        public ChatConversationDto CurrentConversation { get; set; }
        public ChatContactDto CurrentContact { get; set; }
        public Guid? CurrentConversationId { get; set; }

        // Contact list management
        public List<ChatContactDto> ContactList { get; set; } = new List<ChatContactDto>();
        public Dictionary<ChatContactDto, string> ContactActiveStates { get; set; } = new Dictionary<ChatContactDto, string>();

        // Message composition
        public ChatMessageDto ReplyingToMessage { get; set; }
        public List<MessageFileDto> UploadedFiles { get; set; } = new List<MessageFileDto>();
        public string MessageText { get; set; } = string.Empty;

        // UI state
        public ElementReference MessageTextArea { get; set; }
        public bool SendOnEnter { get; set; } = true;
        public bool ShowInfoBox { get; set; } = false;
        public string SearchValue { get; set; } = string.Empty;

        // User information
        public CurrentUserDto CurrentUser { get; set; }

        // Callback for state change notification
        public Func<Task> OnChange { get; set; }

        /// <summary>
        /// Notify listeners that state has changed
        /// </summary>
        public async Task NotifyStateChangedAsync()
        {
            if (OnChange != null)
            {
                await OnChange.Invoke();
            }
        }

        /// <summary>
        /// Reset all state to default values
        /// </summary>
        public void Reset()
        {
            CurrentConversation = null;
            CurrentContact = null;
            CurrentConversationId = null;
            ContactList?.Clear();
            ContactActiveStates?.Clear();
            ReplyingToMessage = null;
            UploadedFiles?.Clear();
            MessageText = string.Empty;
            SearchValue = string.Empty;
            ShowInfoBox = false;
        }

        /// <summary>
        /// Get snapshot of current state for operations
        /// </summary>
        public ChatStateSnapshot GetSnapshot()
        {
            return new ChatStateSnapshot
            {
                CurrentConversationId = CurrentConversationId,
                CurrentContactUserId = CurrentContact?.UserId,
                MessageCount = CurrentConversation?.Messages?.Count ?? 0,
                ContactCount = ContactList?.Count ?? 0,
                HasUnreadMessages = ContactList?.Any(c => c.UnreadMessageCount > 0) ?? false
            };
        }
    }

    /// <summary>
    /// Snapshot of chat state for logging/debugging
    /// </summary>
    public class ChatStateSnapshot
    {
        public Guid? CurrentConversationId { get; set; }
        public Guid? CurrentContactUserId { get; set; }
        public int MessageCount { get; set; }
        public int ContactCount { get; set; }
        public bool HasUnreadMessages { get; set; }
    }

    /// <summary>
    /// Pagination state for messages and conversations
    /// </summary>
    public class PaginationState
    {
        public int MessageSkipCount { get; set; } = 0;
        public int ConversationSkipCount { get; set; } = 0;
        public bool HasMoreMessages { get; set; } = true;
        public bool HasMoreConversations { get; set; } = true;
        public bool IsLoadingMoreMessages { get; set; } = false;
        public bool IsLoadingMoreConversations { get; set; } = false;
        public Guid? CurrentConversationId { get; set; }
        private const int MessagesPageSize = 10;
        private const int ConversationsPageSize = 10;

        public void ResetMessages()
        {
            MessageSkipCount = 0;
            HasMoreMessages = true;
            IsLoadingMoreMessages = false;
        }

        public void ResetConversations()
        {
            ConversationSkipCount = 0;
            HasMoreConversations = true;
            IsLoadingMoreConversations = false;
        }
    }

    /// <summary>
    /// Modal visibility state
    /// </summary>
    public class ModalState
    {
        public bool ShowCreateDirectModal { get; set; }
        public bool ShowCreateGroupModal { get; set; }
        public bool ShowCreateProjectModal { get; set; }
        public bool ShowCreateTaskModal { get; set; }
        public bool ShowForwardMessageModal { get; set; }
        public bool ShowCreateTaskFromMessageModal { get; set; }
    }

    /// <summary>
    /// Loading state for various operations
    /// </summary>
    public class LoadingState
    {
        public bool IsLoadingMessages { get; set; }
        public bool IsSendingMessage { get; set; }
        public bool IsUploadingFile { get; set; }
        public bool IsCreatingConversation { get; set; }

        public bool IsAnyOperationInProgress()
        {
            return IsLoadingMessages || IsSendingMessage || IsUploadingFile || IsCreatingConversation;
        }
    }

    /// <summary>
    /// User information container
    /// </summary>
    public class CurrentUserDto
    {
        public Guid? Id { get; set; }
        public string UserName { get; set; }
        public string Name { get; set; }
        public string SurName { get; set; }
    }
}
