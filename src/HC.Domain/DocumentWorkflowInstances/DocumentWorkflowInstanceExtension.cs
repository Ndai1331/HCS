using System;
using JetBrains.Annotations;
using Volo.Abp;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HC.DocumentWorkflowInstances;

public class DocumentWorkflowInstanceExtension : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid DocumentWorkflowInstanceId { get; set; }

    public virtual Guid ExtendedByUserId { get; set; }

    public virtual int ExtensionBusinessDays { get; set; }

    public virtual DateTime PreviousFinishedAt { get; set; }

    public virtual DateTime NewFinishedAt { get; set; }

    [NotNull]
    public virtual string Reason { get; set; } = null!;

    [CanBeNull]
    public virtual string? PreviousStatus { get; set; }

    [CanBeNull]
    public virtual string? NewStatus { get; set; }

    protected DocumentWorkflowInstanceExtension()
    {
    }

    public DocumentWorkflowInstanceExtension(
        Guid id,
        Guid documentWorkflowInstanceId,
        Guid extendedByUserId,
        int extensionBusinessDays,
        DateTime previousFinishedAt,
        DateTime newFinishedAt,
        string reason,
        string? previousStatus,
        string? newStatus)
    {
        Id = id;
        Check.NotNull(reason, nameof(reason));
        Check.Length(reason, nameof(reason), DocumentWorkflowInstanceExtensionConsts.ReasonMaxLength);
        DocumentWorkflowInstanceId = documentWorkflowInstanceId;
        ExtendedByUserId = extendedByUserId;
        ExtensionBusinessDays = extensionBusinessDays;
        PreviousFinishedAt = previousFinishedAt;
        NewFinishedAt = newFinishedAt;
        Reason = reason;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
    }
}
