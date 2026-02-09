using System.Collections.Generic;
using System.Threading.Tasks;
using Blazorise;
using Blazorise.DataGrid;
using HC.ProjectTasks;
using HC.Shared;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;

namespace HC.Blazor.Components.TaskTree;

public partial class TaskTree
{
    /// <summary>
    /// The list of tasks to display at this level
    /// </summary>
    [Parameter]
    public IReadOnlyList<ProjectTaskWithNavigationPropertiesDto> Tasks { get; set; } = new List<ProjectTaskWithNavigationPropertiesDto>();

    /// <summary>
    /// The current nesting level (0 = root level)
    /// </summary>
    [Parameter]
    public int Level { get; set; } = 0;

    /// <summary>
    /// Dictionary mapping parent task codes to their child tasks
    /// </summary>
    [Parameter]
    public Dictionary<string, List<ProjectTaskWithNavigationPropertiesDto>> ChildTasksByParentCode { get; set; } = new();

    /// <summary>
    /// Set of currently expanded task codes
    /// </summary>
    [Parameter]
    public HashSet<string> ExpandedTasks { get; set; } = new();

    /// <summary>
    /// Callback invoked when a task is expanded/collapsed
    /// </summary>
    [Parameter]
    public EventCallback<(string TaskCode, bool IsExpanded)> OnTaskExpanded { get; set; }

    /// <summary>
    /// Function to get count of child tasks for a task
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, int> GetChildTaskCount { get; set; } = default!;

    /// <summary>
    /// Function to get child tasks for a task
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, List<ProjectTaskWithNavigationPropertiesDto>> GetChildTasksForTask { get; set; } = default!;

    /// <summary>
    /// Function to get task code
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, string> GetTaskCode { get; set; } = default!;

    /// <summary>
    /// Function to get task title
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, string> GetTaskTitle { get; set; } = default!;

    /// <summary>
    /// Function to get task start date
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, System.DateTime> GetTaskStartDate { get; set; } = default!;

    /// <summary>
    /// Function to get task due date
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, System.DateTime> GetTaskDueDate { get; set; } = default!;

    /// <summary>
    /// Function to get task status
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, ProjectTaskStatus> GetTaskStatus { get; set; } = default!;

    /// <summary>
    /// Function to get task priority
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, ProjectTaskPriority> GetTaskPriority { get; set; } = default!;

    /// <summary>
    /// Function to get task progress percent
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, int> GetTaskProgressPercent { get; set; } = default!;

    /// <summary>
    /// Function to get task detail URL
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, string> GetTaskDetailUrl { get; set; } = default!;

    /// <summary>
    /// Function to get parent task ID
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, string> GetTaskParentTaskId { get; set; } = default!;

    /// <summary>
    /// Function to get parent task title
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskWithNavigationPropertiesDto, string> GetParentTaskTitle { get; set; } = default!;

    /// <summary>
    /// Caption for Code column
    /// </summary>
    [Parameter]
    public string CodeCaption { get; set; } = "Code";

    /// <summary>
    /// Caption for Title column
    /// </summary>
    [Parameter]
    public string TitleCaption { get; set; } = "Title";

    /// <summary>
    /// Caption for StartDate column
    /// </summary>
    [Parameter]
    public string StartDateCaption { get; set; } = "StartDate";

    /// <summary>
    /// Caption for DueDate column
    /// </summary>
    [Parameter]
    public string DueDateCaption { get; set; } = "DueDate";

    /// <summary>
    /// Caption for Status column
    /// </summary>
    [Parameter]
    public string StatusCaption { get; set; } = "Status";

    /// <summary>
    /// Caption for Priority column
    /// </summary>
    [Parameter]
    public string PriorityCaption { get; set; } = "Priority";

    /// <summary>
    /// Caption for ProgressPercent column
    /// </summary>
    [Parameter]
    public string ProgressPercentCaption { get; set; } = "ProgressPercent";

    /// <summary>
    /// Label for parent task title
    /// </summary>
    [Parameter]
    public string ParentTaskTitleLabel { get; set; } = "ParentTaskTitle";

    /// <summary>
    /// Function to get status badge color
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskStatus, Color> GetStatusBadgeColor { get; set; } = default!;

    /// <summary>
    /// Function to get priority badge color
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskPriority, Color> GetPriorityBadgeColor { get; set; } = default!;

    /// <summary>
    /// Function to get status text
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskStatus, string> GetStatusText { get; set; } = default!;

    /// <summary>
    /// Function to get priority text
    /// </summary>
    [Parameter]
    public System.Func<ProjectTaskPriority, string> GetPriorityText { get; set; } = default!;

    /// <summary>
    /// Check if a task is currently expanded
    /// </summary>
    private bool IsTaskExpanded(ProjectTaskWithNavigationPropertiesDto task)
    {
        var taskCode = GetTaskCode(task);
        return ExpandedTasks.Contains(taskCode);
    }

    /// <summary>
    /// Toggle the expand/collapse state of a task
    /// </summary>
    private async Task ToggleTaskDetails(ProjectTaskWithNavigationPropertiesDto task)
    {
        var taskCode = GetTaskCode(task);
        
        if (ExpandedTasks.Contains(taskCode))
        {
            ExpandedTasks.Remove(taskCode);
        }
        else
        {
            ExpandedTasks.Add(taskCode);
        }

        // Notify parent of the change
        await OnTaskExpanded.InvokeAsync((taskCode, IsTaskExpanded(task)));
        
        Logger.LogInformation("Task {TaskCode} toggled to {IsExpanded}", taskCode, IsTaskExpanded(task));
    }

    /// <summary>
    /// Handle task expanded event from child components
    /// </summary>
    private async Task HandleTaskExpanded((string TaskCode, bool IsExpanded) eventArgs)
    {
        await OnTaskExpanded.InvokeAsync(eventArgs);
    }

    /// <summary>
    /// Handle detail row trigger - prevent automatic toggling and hide empty detail rows
    /// </summary>
    private bool DetailRowTriggerHandler(DetailRowTriggerEventArgs<ProjectTaskWithNavigationPropertiesDto> e)
    {
        var childCount = GetChildTaskCount(e.Item);
        
        if (childCount == 0)
        {
            // For tasks without children, return false to prevent detail row from showing
            e.Toggleable = false;
            return false;
        }
        else
        {
            // For tasks with children, prevent automatic toggling - only allow manual toggle via chevron icon
            e.Toggleable = false;
            e.DetailRowTriggerType = DetailRowTriggerType.Manual;
            return true;
        }
    }
}
