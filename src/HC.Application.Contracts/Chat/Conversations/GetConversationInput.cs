using System;
using Volo.Abp.Application.Dtos;

namespace HC.Chat.Conversations;

public class GetConversationInput : PagedResultRequestDto
{
    public Guid TargetUserId { get; set; } 
    public Guid? ConversationId { get; set; } 
}
