using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public int StepOrder { get; set; }

    public string ActionType { get; set; }

    public string Status { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime ProcessedAt { get; set; }

    public bool IsCurrent { get; set; }

    public Guid DocumentId { get; set; }

    public Guid? WorkflowStepTemplateId { get; set; }

    public Guid ReceiverUserId { get; set; }

    public Guid? DocumentFileResultId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}