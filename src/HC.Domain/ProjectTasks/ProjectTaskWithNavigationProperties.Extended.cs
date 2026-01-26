using System.Collections.Generic;
using HC.Projects;

namespace HC.ProjectTasks;

public class ProjectTaskWithNavigationProperties : ProjectTaskWithNavigationPropertiesBase
{
    public List<ProjectTaskAssignments.ProjectTaskAssignment> ProjectTaskAssignments { get; set; } = new();
}