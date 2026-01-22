using System;
using HC.Chat.Messages;
using HC.Chat.Conversations;

namespace HC.Chat.Users;

public class ChatContactDto
{
    public Guid UserId { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Username { get; set; }

    public bool HasChatPermission { get; set; }

    public ChatMessageSide LastMessageSide { get; set; }

    public string? LastMessage { get; set; }

    public DateTime? LastMessageDate { get; set; }

    public int UnreadMessageCount { get; set; }
    
    // New properties
    public ConversationType Type { get; set; }
    public string? ConversationName { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedDate { get; set; } // When current user pinned this conversation
    public int MemberCount { get; set; }
    public Guid? ConversationId { get; set; } // For group/project/task conversations
    public string? MemberRole { get; set; } // ADMIN / MEMBER - Role of current user in the conversation
    public Guid? ProjectId { get; set; } // For Project conversations
    public Guid? TaskId { get; set; } // For Task conversations
}
