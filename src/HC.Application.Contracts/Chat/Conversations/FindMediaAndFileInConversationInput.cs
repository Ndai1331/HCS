using System;
using Volo.Abp.Application.Dtos;
namespace HC.Chat.Conversations;

public class FindMediaAndFileInConversationInput : PagedResultRequestDto
{
    public Guid ConversationId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public FileMediaType FileType { get; set; } = FileMediaType.Media;
}




