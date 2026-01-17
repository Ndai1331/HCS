using System;

namespace HC.Chat.Conversations;

/// <summary>
/// Event published when a new conversation is created
/// </summary>
public class ConversationCreatedEto
{
    /// <summary>
    /// Target user ID who should receive the notification
    /// </summary>
    public Guid TargetUserId { get; set; }
    
    /// <summary>
    /// Conversation ID
    /// </summary>
    public Guid ConversationId { get; set; }
    
    /// <summary>
    /// Conversation type (User, Group, Project, Task)
    /// </summary>
    public ConversationType Type { get; set; }
    
    /// <summary>
    /// Conversation name (for Group/Project/Task) or null for User conversation
    /// </summary>
    public string? ConversationName { get; set; }
    
    /// <summary>
    /// Creator user ID
    /// </summary>
    public Guid CreatorUserId { get; set; }
    
    /// <summary>
    /// Creator username
    /// </summary>
    public string CreatorUserName { get; set; }
    
    /// <summary>
    /// Creator name
    /// </summary>
    public string CreatorName { get; set; }
    
    /// <summary>
    /// Creator surname
    /// </summary>
    public string CreatorSurname { get; set; }
}
