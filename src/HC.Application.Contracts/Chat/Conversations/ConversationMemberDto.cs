using System;
using HC.Chat.Users;

namespace HC.Chat.Conversations;

public class ConversationMemberDto
{
    public Guid Id { get; set; }
    public Guid ConversationId { get; set; }
    public Guid UserId { get; set; }
    public string Role { get; set; }
    public bool IsActive { get; set; }
    public bool IsPinned { get; set; }
    public DateTime? PinnedDate { get; set; }
    public int UnreadMessageCount { get; set; }
    public DateTime JoinedDate { get; set; }
    public ChatTargetUserInfo UserInfo { get; set; }
}
