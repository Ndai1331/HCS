using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Chat.Conversations;

public class LeaveConversationInput
{
    [Required]
    public Guid ConversationId { get; set; }
}
