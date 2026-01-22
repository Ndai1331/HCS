using System;

namespace HC.Chat.Users;

public class ChatTargetUserInfo
{
    public Guid UserId { get; set; }

    public string? Name { get; set; }

    public string? Surname { get; set; }

    public string? Username { get; set; }
}
