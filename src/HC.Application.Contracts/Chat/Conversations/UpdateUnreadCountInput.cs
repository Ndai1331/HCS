using System;

namespace HC.Chat.Conversations;

public class UpdateUnreadCountInput
{
    public Guid ConversationId { get; set; }
    public int IncrementBy { get; set; } = 1;
}
