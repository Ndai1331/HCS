using System.Collections.Generic;
using HC.Projects;
using HC.ProjectTaskAssignments;

namespace HC.ProjectTasks;

public class ProjectTaskWithNavigationProperties : ProjectTaskWithNavigationPropertiesBase
{
    public List<ProjectTaskAssignment> ProjectTaskAssignments { get; set; } = new();
    
    public int ProjectTaskDocumentsCount { get; set; }
    
    public int ChildTaskCount { get; set; }
}
