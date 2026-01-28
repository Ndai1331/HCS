using Volo.Abp.Application.Dtos;
using System;

namespace HC.Documents;

public abstract class GetDocumentsInputBase : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    public string? No { get; set; }

    public string? Title { get; set; }

    public string? CurrentStatus { get; set; }

    public DateTime? CompletedTimeMin { get; set; }

    public DateTime? CompletedTimeMax { get; set; }

    public string? StorageNumber { get; set; }

    public DateTime? IncommingDateMin { get; set; }

    public DateTime? IncommingDateMax { get; set; }

    public Guid? FieldId { get; set; }

    public Guid? UnitId { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? StatusId { get; set; }

    public Guid? TypeId { get; set; }

    public Guid? UrgencyLevelId { get; set; }

    public Guid? SecrecyLevelId { get; set; }

    public GetDocumentsInputBase()
    {
    }
}