using System;
using Volo.Abp.Application.Dtos;
using System.Collections.Generic;

namespace HC.Chat.Conversations;

public class FindConversationInput : PagedResultRequestDto
{
    public List<Guid> UserIds { get; set; } = new List<Guid>();
    public ConversationType Type { get; set; } = ConversationType.User;
}
