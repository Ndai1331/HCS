using System;

namespace HC.ProjectTasks;

public class GetProjectTasksInput : GetProjectTasksInputBase
{
    public Guid? UserId { get; set; }
}