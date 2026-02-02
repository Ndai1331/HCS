using System;

namespace HC.Chat.Messages;

public class ChatMessageRdto
{
    public Guid SenderUserId { get; set; }
    public Guid Id { get; set; }
    public Guid? ConversationId { get; set; }
    public string? SenderUsername { get; set; }
    public string? SenderName { get; set; }
    public string? SenderSurname { get; set; }
    public string? Text { get; set; }
    public bool IsCrossTabMessage { get; set; }
    public DateTime? MessageDate { get; set; }
}
