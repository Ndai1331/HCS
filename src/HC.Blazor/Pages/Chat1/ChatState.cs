using System;
using System.Collections.Generic;
using HC.Chat;
using HC.Chat.Conversations;
using HC.Chat.Messages;
using HC.Chat.Users;

namespace HC.Blazor.Pages.Chat1;

/// <summary>
/// State class consolidating chat-related properties.
/// Centralizes state management for cleaner component architecture.
/// </summary>
public class ChatState
{
    /// <summary>
    /// Current active conversation.
    /// </summary>
    public ChatConversationDto CurrentConversation { get; set; }

    /// <summary>
    /// Current active contact.
    /// </summary>
    public ChatContactDto CurrentContact { get; set; }

    /// <summary>
    /// Message being replied to (if any).
    /// </summary>
    public ChatMessageDto ReplyingToMessage { get; set; }

    /// <summary>
    /// Files uploaded for current message.
    /// </summary>
    public List<MessageFileDto> UploadedFiles { get; set; } = new List<MessageFileDto>();

    /// <summary>
    /// Current message text.
    /// </summary>
    public string MessageText { get; set; } = string.Empty;

    /// <summary>
    /// Whether "Send on Enter" is enabled.
    /// </summary>
    public bool SendOnEnter { get; set; } = true;

    /// <summary>
    /// Whether the info box is shown.
    /// </summary>
    public bool ShowInfoBox { get; set; } = false;

    /// <summary>
    /// Search text in contacts.
    /// </summary>
    public string SearchValue { get; set; } = string.Empty;
}

/// <summary>
/// Pagination state class consolidating pagination-related properties.
/// </summary>
public class PaginationState
{
    /// <summary>
    /// Current skip count for messages pagination.
    /// </summary>
    public int MessageSkipCount { get; set; } = 0;

    /// <summary>
    /// Current skip count for conversations pagination.
    /// </summary>
    public int ConversationSkipCount { get; set; } = 0;

    /// <summary>
    /// Whether there are more messages to load.
    /// </summary>
    public bool HasMoreMessages { get; set; } = true;

    /// <summary>
    /// Whether there are more conversations to load.
    /// </summary>
    public bool HasMoreConversations { get; set; } = true;

    /// <summary>
    /// Whether currently loading more messages.
    /// </summary>
    public bool IsLoadingMoreMessages { get; set; } = false;

    /// <summary>
    /// Whether currently loading more conversations.
    /// </summary>
    public bool IsLoadingMoreConversations { get; set; } = false;

    /// <summary>
    /// Current conversation ID.
    /// </summary>
    public Guid? CurrentConversationId { get; set; }
}

/// <summary>
/// Modal state class consolidating modal visibility flags.
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
