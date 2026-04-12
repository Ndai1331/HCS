using System;
using Volo.Abp.Application.Dtos;

namespace HC.Chat.Conversations;

public class GetMessageContextInput : PagedResultRequestDto
{
    public Guid ConversationId { get; set; }
    public Guid MessageId { get; set; }

    public int BeforeCount { get; set; } = 20;
    public int AfterCount { get; set; } = 20;
}
