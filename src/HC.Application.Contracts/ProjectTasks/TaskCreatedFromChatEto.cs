using System;
using System.Collections.Generic;

namespace HC.ProjectTasks;

/// <summary>
/// Event published when a task is created from chat message
/// </summary>
public class TaskCreatedFromChatEto
{
    public Guid TaskId { get; set; }
    public string TaskCode { get; set; }
    public string TaskTitle { get; set; }
    public Guid CreatorUserId { get; set; }
    public string CreatorName { get; set; }
    public List<Guid> AssigneeUserIds { get; set; } = new();
}
