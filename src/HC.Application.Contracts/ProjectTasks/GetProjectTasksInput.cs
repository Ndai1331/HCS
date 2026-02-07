using Volo.Abp.Application.Dtos;
using System;

namespace HC.ProjectTasks;

public abstract class GetProjectTasksInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public bool OnlyParentTasks { get; set; }

    public bool OnlyChildTasks { get; set; }

    public string? ParentTaskId { get; set; }

    public string? Code { get; set; }

    public string? Title { get; set; }

    public string? Description { get; set; }

    public DateTime? StartDateMin { get; set; }

    public DateTime? StartDateMax { get; set; }

    public DateTime? DueDateMin { get; set; }

    public DateTime? DueDateMax { get; set; }

    public string? Priority { get; set; }

    public string? Status { get; set; }

    public int? ProgressPercentMin { get; set; }

    public int? ProgressPercentMax { get; set; }

    public Guid? ProjectId { get; set; }

    public GetProjectTasksInputBase()
    {
    }
}