using System;
using System.ComponentModel.DataAnnotations;
using Volo.Abp.Validation;

namespace HC.Chat.Messages;

public class SendMessageInput
{
    /// <summary>
    /// Required: ConversationId for ALL message types (User, Group, Project, Task)
    /// </summary>
    [Required]
    public Guid ConversationId { get; set; }

    [Required]
    [DynamicStringLength(typeof(ChatMessageConsts),nameof(ChatMessageConsts.MaxTextLength), nameof(ChatMessageConsts.MinTextLength))]
    public string? Message { get; set; }
}
