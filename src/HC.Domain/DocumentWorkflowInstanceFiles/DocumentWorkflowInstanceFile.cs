using HC.DocumentFiles;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class DocumentWorkflowInstanceFileBase : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid DocumentWorkflowInstanceId { get; set; }

    public virtual Guid? TenantId { get; set; }

    public Guid DocumentFileId { get; set; }

    protected DocumentWorkflowInstanceFileBase()
    {
    }

    public DocumentWorkflowInstanceFileBase(Guid id, Guid documentWorkflowInstanceId, Guid documentFileId)
    {
        Id = id;
        DocumentWorkflowInstanceId = documentWorkflowInstanceId;
        DocumentFileId = documentFileId;
    }
}