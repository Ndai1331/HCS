using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Chat.Conversations;

public class SetMemberRoleInput
{
    [Required]
    public Guid ConversationId { get; set; }
    
    [Required]
    public Guid UserId { get; set; }
    
    [Required]
    public string Role { get; set; } = "MEMBER";
}
