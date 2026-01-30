using Volo.Abp.Application.Dtos;
using System;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentExcelDownloadDtoBase
{
    public string DownloadToken { get; set; } = null!;
    public string? FilterText { get; set; }

    public int? StepOrderMin { get; set; }

    public int? StepOrderMax { get; set; }

    public string? ActionType { get; set; }

    public string? Status { get; set; }

    public DateTime? AssignedAtMin { get; set; }

    public DateTime? AssignedAtMax { get; set; }

    public DateTime? ProcessedAtMin { get; set; }

    public DateTime? ProcessedAtMax { get; set; }

    public bool? IsCurrent { get; set; }

    public Guid? DocumentId { get; set; }

    public Guid? WorkflowStepTemplateId { get; set; }

    public Guid? ReceiverUserId { get; set; }

    public DocumentAssignmentExcelDownloadDtoBase()
    {
    }
}