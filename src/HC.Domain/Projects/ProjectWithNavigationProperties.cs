using HC.Departments;
using System;
using System.Collections.Generic;
using HC.Projects;
using HC.ProjectMembers;

namespace HC.Projects;

public abstract class ProjectWithNavigationPropertiesBase
{
    public Project Project { get; set; } = null!;
    public Department? OwnerDepartment { get; set; }
    public List<ProjectMember>? ProjectMembers { get; set; }

    // Navigation aggregates for list views (avoid loading full collections)
    public int ProjectMemberCount { get; set; }
    public int ProjectTaskCount { get; set; }
}