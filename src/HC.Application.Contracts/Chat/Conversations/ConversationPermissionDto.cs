namespace HC.Chat.Conversations;

public class ConversationPermissionDto
{
    public string MyRole { get; set; } = "MEMBER";
    public bool CanLeave { get; set; }
    public bool CanDelete { get; set; }
    public bool CanAddMembers { get; set; }
    public bool CanRemoveMembers { get; set; }
    public bool CanChangeRoles { get; set; }
    public bool IsOnlyAdmin { get; set; }
    public int AdminCount { get; set; }
    public int MemberCount { get; set; }
}
