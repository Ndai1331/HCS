using HC.Documents;
using HC.DocumentFiles;
using HC.WorkflowStepTemplates;
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

namespace HC.DocumentAssignments;

public abstract class DocumentAssignmentBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual int StepOrder { get; set; }

    [NotNull]
    public virtual string ActionType { get; set; }

    [NotNull]
    public virtual string Status { get; set; }

    public virtual DateTime AssignedAt { get; set; }

    public virtual DateTime ProcessedAt { get; set; }

    public virtual bool IsCurrent { get; set; }

    public Guid DocumentId { get; set; }

    public Guid? WorkflowStepTemplateId { get; set; }

    public Guid ReceiverUserId { get; set; }

    public Guid? DocumentFileResultId { get; set; }

    protected DocumentAssignmentBase()
    {
    }

    public DocumentAssignmentBase(Guid id, Guid documentId, Guid? workflowStepTemplateId, Guid receiverUserId, int stepOrder, string actionType, string status, DateTime assignedAt, DateTime processedAt, bool isCurrent)
    {
        Id = id;
        if (stepOrder < DocumentAssignmentConsts.StepOrderMinLength)
        {
            throw new ArgumentOutOfRangeException(nameof(stepOrder), stepOrder, "The value of 'stepOrder' cannot be lower than " + DocumentAssignmentConsts.StepOrderMinLength);
        }

        if (stepOrder > DocumentAssignmentConsts.StepOrderMaxLength)
        {
            throw new ArgumentOutOfRangeException(nameof(stepOrder), stepOrder, "The value of 'stepOrder' cannot be greater than " + DocumentAssignmentConsts.StepOrderMaxLength);
        }

        Check.NotNull(actionType, nameof(actionType));
        Check.Length(actionType, nameof(actionType), DocumentAssignmentConsts.ActionTypeMaxLength, 0);
        Check.NotNull(status, nameof(status));
        Check.Length(status, nameof(status), DocumentAssignmentConsts.StatusMaxLength, 0);
        StepOrder = stepOrder;
        ActionType = actionType;
        Status = status;
        AssignedAt = assignedAt;
        ProcessedAt = processedAt;
        IsCurrent = isCurrent;
        DocumentId = documentId;
        WorkflowStepTemplateId = workflowStepTemplateId;
        ReceiverUserId = receiverUserId;
    }
}