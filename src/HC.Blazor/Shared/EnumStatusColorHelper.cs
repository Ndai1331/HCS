using System;
using System.Collections.Generic;
using System.Linq;
using Blazorise;
using HC.CalendarEventParticipants;
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
}