using System;
using Volo.Abp.Application.Dtos;

namespace HC.Chat.Conversations;

public class SearchConversationMessagesInput : PagedResultRequestDto
{
    public Guid ConversationId { get; set; }
    public string Keyword { get; set; } = string.Empty;
}
