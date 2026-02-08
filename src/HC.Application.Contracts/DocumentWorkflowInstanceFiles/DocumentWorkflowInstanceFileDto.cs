using System;
using System.Collections.Generic;
using Volo.Abp.Application.Dtos;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileDtoBase : FullAuditedEntityDto<Guid>
{
    public Guid DocumentWorkflowInstanceId { get; set; }

    public Guid DocumentFileId { get; set; }
}