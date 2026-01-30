using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;
using Volo.Abp.Domain.Entities;

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentUpdateDtoBase : IHasConcurrencyStamp
{
    [Range(DocumentAssignmentConsts.StepOrderMinLength, DocumentAssignmentConsts.StepOrderMaxLength)]
    public int StepOrder { get; set; }

    [Required]
    [StringLength(DocumentAssignmentConsts.ActionTypeMaxLength)]
    public string ActionType { get; set; }

    [Required]
    [StringLength(DocumentAssignmentConsts.StatusMaxLength)]
    public string Status { get; set; }

    public DateTime AssignedAt { get; set; }

    public DateTime ProcessedAt { get; set; }

    public bool IsCurrent { get; set; }

    public Guid DocumentId { get; set; }

    public Guid? WorkflowStepTemplateId { get; set; }

    public Guid ReceiverUserId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
}