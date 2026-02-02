using System;
using System.Collections.Generic;

namespace HC.Chat.Messages;

public class ChatMessageDto
{
    public Guid Id { get; set; }
    
    public string? Message { get; set; }

    public DateTime MessageDate { get; set; }

    public bool IsRead { get; set; }

    public DateTime ReadDate { get; set; }

    public ChatMessageSide Side { get; set; }
    
    public bool IsPinned { get; set; }
    public DateTime? PinnedDate { get; set; }
    public Guid? ReplyToMessageId { get; set; }
    public Guid? ConversationId { get; set; }
    public ChatMessageDto? ReplyToMessage { get; set; }
    public Guid? ForwardedFromMessageId { get; set; }
    public ChatMessageDto? ForwardedFromMessage { get; set; } 
    public List<MessageFileDto>? Files { get; set; }
    
    public Guid? SenderUserId { get; set; }
    public string? SenderName { get; set; }
    public string? SenderSurname { get; set; }
    public string? SenderUsername { get; set; }
    
    public bool IsSending { get; set; }
}
