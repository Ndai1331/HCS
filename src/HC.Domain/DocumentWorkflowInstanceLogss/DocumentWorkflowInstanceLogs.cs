using HC.DocumentAssignments;
using Volo.Abp.Identity;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class DocumentWorkflowInstanceLogsBase : FullAuditedEntity<Guid>, IMultiTenant
{
    public virtual Guid DocumentWorkflowInstanceId { get; set; }

    public virtual Guid? TenantId { get; set; }

    [NotNull]
    public virtual string Action { get; set; }

    [CanBeNull]
    public virtual string? ActorRole { get; set; }

    [CanBeNull]
    public virtual string? FromStatus { get; set; }

    [CanBeNull]
    public virtual string? ToStatus { get; set; }

    [CanBeNull]
    public virtual string? Note { get; set; }

    public Guid? DocumentAssignmentId { get; set; }

    public Guid? ActorUserId { get; set; }

    protected DocumentWorkflowInstanceLogsBase()
    {
    }

    public DocumentWorkflowInstanceLogsBase(Guid id, Guid documentWorkflowInstanceId, Guid? documentAssignmentId, Guid? actorUserId, string action, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null)
    {
        Id = id;
        Check.NotNull(action, nameof(action));
        Check.Length(action, nameof(action), DocumentWorkflowInstanceLogsConsts.ActionMaxLength, 0);
        Check.Length(actorRole, nameof(actorRole), DocumentWorkflowInstanceLogsConsts.ActorRoleMaxLength, 0);
        Check.Length(fromStatus, nameof(fromStatus), DocumentWorkflowInstanceLogsConsts.FromStatusMaxLength, 0);
        Check.Length(toStatus, nameof(toStatus), DocumentWorkflowInstanceLogsConsts.ToStatusMaxLength, 0);
        DocumentWorkflowInstanceId = documentWorkflowInstanceId;
        Action = action;
        ActorRole = actorRole;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Note = note;
        DocumentAssignmentId = documentAssignmentId;
        ActorUserId = actorUserId;
    }
}