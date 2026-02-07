using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsDtoBase : FullAuditedEntityDto<Guid>
{
    public Guid DocumentWorkflowInstanceId { get; set; }

    public string Action { get; set; }

    public string? ActorRole { get; set; }

    public string? FromStatus { get; set; }

    public string? ToStatus { get; set; }

    public string? Note { get; set; }

    public Guid? DocumentAssignmentId { get; set; }

    public Guid? ActorUserId { get; set; }
}