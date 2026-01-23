using System;
using Volo.Abp.Application.Dtos;
namespace HC.Chat.Conversations;

public class FindMessageInConversationInput : PagedResultRequestDto
{
    public Guid ConversationId { get; set; }
    public string MessageText { get; set; } = string.Empty;
}
