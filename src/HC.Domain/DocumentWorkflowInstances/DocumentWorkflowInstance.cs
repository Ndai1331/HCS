using HC.Documents;
using HC.Workflows;
using HC.WorkflowTemplates;
using HC.WorkflowStepTemplates;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using Volo.Abp;

namespace HC.DocumentWorkflowInstances;

public abstract class DocumentWorkflowInstanceBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Status { get; set; }

    public virtual DateTime StartedAt { get; set; }

    public virtual DateTime FinishedAt { get; set; }

    public Guid DocumentId { get; set; }

    public Guid WorkflowId { get; set; }

    public Guid WorkflowTemplateId { get; set; }

    public Guid CurrentStepId { get; set; }

    /// <summary>
    /// JSON array of <see cref="WorkflowStepTemplate"/> Ids in submission order, captured at submit/resubmit.
    /// Runtime and UI must use this instead of re-querying all active steps on <see cref="WorkflowTemplateId"/>.
    /// </summary>
    [CanBeNull]
    public virtual string? CommittedStepTemplateIdsJson { get; set; }

    /// <summary>
    /// JSON array of VIEW <see cref="WorkflowStepTemplate"/> Ids that have been reached (unlocked for read access).
    /// </summary>
    [CanBeNull]
    public virtual string? UnlockedViewStepTemplateIdsJson { get; set; }

    /// <summary>When status became OVERDUE (signing deadline passed).</summary>
    public virtual DateTime? OverdueAt { get; set; }

    /// <summary>Number of deadline extensions applied to this instance.</summary>
    public virtual int ExtensionCount { get; set; }

    /// <summary>Total business days added via extensions.</summary>
    public virtual int TotalExtensionBusinessDays { get; set; }

    public ICollection<DocumentWorkflowInstanceFile> DocumentWorkflowInstanceFiles { get; private set; }

    public ICollection<DocumentWorkflowInstanceLogs> DocumentWorkflowInstanceLogss { get; private set; }

    protected DocumentWorkflowInstanceBase()
    {
    }

    public DocumentWorkflowInstanceBase(Guid id, Guid documentId, Guid workflowId, Guid workflowTemplateId, Guid currentStepId, string status, DateTime startedAt, DateTime finishedAt)
    {
        Id = id;
        Check.NotNull(status, nameof(status));
        Check.Length(status, nameof(status), DocumentWorkflowInstanceConsts.StatusMaxLength, 0);
        Status = status;
        StartedAt = startedAt;
        FinishedAt = finishedAt;
        DocumentId = documentId;
        WorkflowId = workflowId;
        WorkflowTemplateId = workflowTemplateId;
        CurrentStepId = currentStepId;
        DocumentWorkflowInstanceFiles = new Collection<DocumentWorkflowInstanceFile>();
        DocumentWorkflowInstanceLogss = new Collection<DocumentWorkflowInstanceLogs>();
    }
}