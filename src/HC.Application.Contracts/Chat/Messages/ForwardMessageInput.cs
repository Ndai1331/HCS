using System;
using System.ComponentModel.DataAnnotations;

namespace HC.Chat.Messages;

/// <summary>
/// Input for forwarding a message to another conversation
/// </summary>
public class ForwardMessageInput
{
    /// <summary>
    /// The message ID to forward
    /// </summary>
    [Required]
    public Guid MessageId { get; set; }

    /// <summary>
    /// The target conversation ID to forward the message to
    /// </summary>
    [Required]
    public Guid TargetConversationId { get; set; }

    /// <summary>
    /// Optional: Additional comment to add when forwarding
    /// </summary>
    public string? AdditionalComment { get; set; }
}
