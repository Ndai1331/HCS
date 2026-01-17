using System;

namespace HC.Chat.Conversations;

/// <summary>
/// Input for creating a user-to-user conversation (2 members)
/// </summary>
public class CreateUserConversationInput
{
    /// <summary>
    /// Target user ID to chat with
    /// </summary>
    public Guid TargetUserId { get; set; }
    
    /// <summary>
    /// Optional custom name for the conversation
    /// If not provided, will use default naming
    /// </summary>
    public string? Name { get; set; }
}
