using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsUpdateDtoBase
{
    public Guid DocumentWorkflowInstanceId { get; set; }

    [Required]
    [StringLength(DocumentWorkflowInstanceLogsConsts.ActionMaxLength)]
    public string Action { get; set; }

    [StringLength(DocumentWorkflowInstanceLogsConsts.ActorRoleMaxLength)]
    public string? ActorRole { get; set; }

    [StringLength(DocumentWorkflowInstanceLogsConsts.FromStatusMaxLength)]
    public string? FromStatus { get; set; }

    [StringLength(DocumentWorkflowInstanceLogsConsts.ToStatusMaxLength)]
    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    public Guid? DocumentAssignmentId { get; set; }

    public Guid? ActorUserId { get; set; }
}