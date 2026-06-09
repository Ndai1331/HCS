using System.Collections.Generic;
using HC.Chat.Authorization;

namespace HC.Permissions;


/// <summary>
/// Permissions granted implicitly when <see cref="HCPermissions.Workspace.Default"/> is assigned.
/// Covers all APIs used by the Workspace (Index) page.
/// </summary>
public static class WorkspaceImpliedPermissions
{
    public static IReadOnlySet<string> All { get; } = new HashSet<string>
    {
        HCPermissions.Projects.Default,
        HCPermissions.Projects.Create,
        HCPermissions.ProjectTasks.Default,
        HCPermissions.ProjectTasks.Create,
        HCPermissions.ProjectTasks.Edit,
        HCPermissions.ProjectTasks.Delete,
        HCPermissions.ProjectMembers.Default,
        HCPermissions.ProjectTaskAssignments.Default,
        HCPermissions.ProjectTaskAssignments.Create,
        HCPermissions.ProjectTaskAssignments.Edit,
        HCPermissions.ProjectTaskAssignments.Delete,
        HCPermissions.ProjectTaskDocuments.Default,
        HCPermissions.ProjectTaskDocuments.Create,
        HCPermissions.ProjectTaskDocuments.Delete,
        HCPermissions.CalendarEvents.Default,
        HCPermissions.CalendarEventParticipants.Default,
        HCPermissions.Documents.Default,
        HCPermissions.DocumentAssignments.Default,
        HCPermissions.DocumentFiles.Default,
        HCPermissions.NotificationReceivers.Default,
        HCPermissions.NotificationReceivers.Edit,
        HCPermissions.DocumentWorkflowInstanceLogss.Default,
        ChatPermissions.Messaging
    };

    public static bool IsImplied(string permissionName)
    {
        return All.Contains(permissionName);
    }
}
