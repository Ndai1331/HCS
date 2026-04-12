using System;

namespace HC.Chat.Messages;

public class MessageSearchHit
{
    public Guid MessageId { get; set; }
    public Guid ConversationId { get; set; }
    public string Snippet { get; set; } = string.Empty;
    public DateTime CreationTime { get; set; }
}
