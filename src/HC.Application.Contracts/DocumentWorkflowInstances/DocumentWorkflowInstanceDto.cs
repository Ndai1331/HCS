using System;
using System.Collections.Generic;
using HC.DocumentWorkflowInstanceFiles;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Domain.Entities;

namespace HC.DocumentWorkflowInstances;

public abstract class DocumentWorkflowInstanceDtoBase : FullAuditedEntityDto<Guid>, IHasConcurrencyStamp
{
    public string Status { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime FinishedAt { get; set; }

    public Guid DocumentId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid WorkflowTemplateId { get; set; }

    public Guid CurrentStepId { get; set; }

    public string ConcurrencyStamp { get; set; } = null!;
    public List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> DocumentWorkflowInstanceFiles { get; set; } = new();
}