using System;

namespace HC.Chat.Conversations;

public class DeleteConversationInput
{
    public Guid TargetUserId { get; set; }
    public Guid? ConversationId { get; set; }
}
