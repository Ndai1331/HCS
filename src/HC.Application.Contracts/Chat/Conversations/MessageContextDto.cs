using System.Collections.Generic;
using HC.Chat.Messages;

namespace HC.Chat.Conversations;

public class MessageContextDto
{
    public ChatMessageDto? AnchorMessage { get; set; }
    public List<ChatMessageDto> BeforeMessages { get; set; } = new();
    public List<ChatMessageDto> AfterMessages { get; set; } = new();
    public bool HasMoreBefore { get; set; }
    public bool HasMoreAfter { get; set; }
}
