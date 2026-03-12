using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Chat.Conversations;

public class TransferAdminAndLeaveInput
{
    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    public Guid NewAdminUserId { get; set; }
}
