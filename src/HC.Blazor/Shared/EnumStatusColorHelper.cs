using System;
using System.Collections.Generic;
using System.Linq;
using Blazorise;
using HC.CalendarEventParticipants;
using HC.ProjectTaskAssignments;
using HC.ProjectTaskDocuments;
using HC.Projects;
using HC.ProjectTasks;
namespace HC.Blazor.Shared;
public class EnumStatusColorHelper
{
    public static Color GetProjectStatusBadgeColor(ProjectStatus projectStatus)
    {
        return projectStatus switch
        {
            ProjectStatus.PLANNING => Color.Info,
            ProjectStatus.IN_PROGRESS => Color.Primary,
            ProjectStatus.COMPLETED => Color.Success,
            ProjectStatus.CANCELLED => Color.Danger,
            _ => Color.Info,
        };
    }

    public static string GetProjectStatusProgressBarColor(ProjectStatus projectStatus)
    {
        return projectStatus switch
        {
            ProjectStatus.PLANNING => "success",
            ProjectStatus.IN_PROGRESS => "warning",
            ProjectStatus.COMPLETED => "brand",
            ProjectStatus.CANCELLED => "danger",
            _ => "success",
        };
    }

    public static Color GetProjectTaskStatusBadgeColor(ProjectTaskStatus projectTaskStatus)
    {
        return projectTaskStatus switch
        {
            ProjectTaskStatus.TODO => Color.Secondary,
            ProjectTaskStatus.IN_PROGRESS => Color.Primary,
            ProjectTaskStatus.WAITING => Color.Warning,
            ProjectTaskStatus.DONE => Color.Success,
            ProjectTaskStatus.CANCELLED => Color.Danger,
            _ => Color.Secondary,
        };
    }

    public static string GetProjectTaskStatusProgressChartColor(ProjectTaskStatus projectTaskStatus)
    {
        return projectTaskStatus switch
        {
            ProjectTaskStatus.TODO => "primary",
            ProjectTaskStatus.IN_PROGRESS => "success",
            ProjectTaskStatus.WAITING => "warning",
            ProjectTaskStatus.DONE => "brand",
            ProjectTaskStatus.CANCELLED => "danger",
            _ => "primary",
        };
    }

    public static Color GetProjectTaskPriorityBadgeColor(ProjectTaskPriority priority)
    {
        return priority switch
        {
            ProjectTaskPriority.LOW => Color.Secondary,
            ProjectTaskPriority.MEDIUM => Color.Info,
            ProjectTaskPriority.HIGH => Color.Warning,
            ProjectTaskPriority.URGENT => Color.Danger,
            _ => Color.Secondary,
        };
    }

    public static Color GetCalendarEventParticipantResponseBadgeColor(ParticipantResponse responseStatus)
    {
        return responseStatus switch
        {
            ParticipantResponse.INVITED => Color.Primary,
            ParticipantResponse.ACCEPTED => Color.Success,
            ParticipantResponse.DECLINED => Color.Danger,
            _ => Color.Primary,
        };
    }

    public static string GetProjectTaskDocumentPurposeBackgroundColor(string? purpose)
    {
        if (!Enum.TryParse<ProjectTaskDocumentPurpose>(purpose, true, out var parsedPurpose))
        {
            return "secondary";
        }

        return GetProjectTaskDocumentPurposeBackgroundColor(parsedPurpose);
    }

    public static string GetProjectTaskDocumentPurposeBackgroundColor(ProjectTaskDocumentPurpose purpose)
    {
        return purpose switch
        {
            ProjectTaskDocumentPurpose.REPORT => "primary",
            ProjectTaskDocumentPurpose.REFERENCE => "secondary",
            _ => "secondary",
        };
    }

    public static string GetProjectTaskDocumentPurposeBackgroundClass(string? purpose)
    {
        return $"bg-{GetProjectTaskDocumentPurposeBackgroundColor(purpose)}";
    }

    public static string GetProjectTaskDocumentPurposeBackgroundClass(ProjectTaskDocumentPurpose purpose)
    {
        return $"bg-{GetProjectTaskDocumentPurposeBackgroundColor(purpose)}";
    }

    public static string GetProjectTaskAssignmentRoleBackgroundColor(string? role)
    {
        if (!Enum.TryParse<ProjectTaskAssignmentRole>(role, true, out var parsedRole))
        {
            return "secondary";
        }

        return GetProjectTaskAssignmentRoleBackgroundColor(parsedRole);
    }

    public static string GetProjectTaskAssignmentRoleBackgroundColor(ProjectTaskAssignmentRole role)
    {
        return role switch
        {
            ProjectTaskAssignmentRole.MAIN => "danger",
            ProjectTaskAssignmentRole.SUPPORT => "info",
            ProjectTaskAssignmentRole.REVIEW => "warning",
            _ => "secondary",
        };
    }

    public static string GetProjectTaskAssignmentRoleBackgroundClass(string? role)
    {
        return $"bg-{GetProjectTaskAssignmentRoleBackgroundColor(role)}";
    }

    public static string GetProjectTaskAssignmentRoleBackgroundClass(ProjectTaskAssignmentRole role)
    {
        return $"bg-{GetProjectTaskAssignmentRoleBackgroundColor(role)}";
    }
}