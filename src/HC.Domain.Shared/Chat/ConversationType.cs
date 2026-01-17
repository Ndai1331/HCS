namespace HC.Chat.Conversations;

/// <summary>
/// Type of conversation
/// </summary>
public enum ConversationType
{
    /// <summary>
    /// User-to-user conversation (2 members, uses Conversation logic)
    /// </summary>
    User = 1,  // Previously 'Direct', now uses Conversation with 2 members
    
    /// <summary>
    /// Group chat with multiple members
    /// </summary>
    Group = 2,
    
    /// <summary>
    /// Project-related chat
    /// </summary>
    Project = 3,
    
    /// <summary>
    /// Task-related chat
    /// </summary>
    Task = 4
}
