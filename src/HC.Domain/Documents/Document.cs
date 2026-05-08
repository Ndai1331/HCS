using HC.MasterDatas;
using HC.Units;
using HC.Workflows;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;
using JetBrains.Annotations;
using Volo.Abp;

namespace HC.Documents;

public abstract class DocumentBase : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    [CanBeNull]
    public virtual string? No { get; set; }

    [NotNull]
    public virtual string Title { get; set; }

    [CanBeNull]
    public virtual string? CurrentStatus { get; set; }

    public virtual DateTime CompletedTime { get; set; }

    [NotNull]
    public virtual string StorageNumber { get; set; }

    public virtual DateTime IncommingDate { get; set; }

    public Guid? FieldId { get; set; }

    public Guid? UnitId { get; set; }

    public Guid? WorkflowId { get; set; }

    public Guid? StatusId { get; set; }

    public Guid TypeId { get; set; }

    public Guid UrgencyLevelId { get; set; }

    public Guid SecrecyLevelId { get; set; }

    public DocumentSourceType SourceType { get; set; } = DocumentSourceType.Archive;

    /// <summary>
    /// User who sent the document (populated when using Send flow; for inbox display).
    /// </summary>
    public Guid? FromUserId { get; set; }

    /// <summary>
    /// Intended individual recipient when send targets a single user (optional denormalized).
    /// </summary>
    public Guid? ReceiverUserId { get; set; }

    /// <summary>
    /// Intended department recipient when send targets a single department (optional denormalized).
    /// </summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>
    /// Intended ABP Identity organization unit recipient when send targets a department.
    /// </summary>
    public Guid? OrganizationUnitId { get; set; }

    /// <summary>
    /// For workflow signing copies (SourceType = Workflow): links to the original Archive/Personal/SentToMe document.
    /// </summary>
    public virtual Guid? ParentDocumentId { get; set; }

    protected DocumentBase()
    {
    }

    public DocumentBase(Guid id, Guid? fieldId, Guid? unitId, Guid? workflowId, Guid? statusId, Guid typeId, Guid urgencyLevelId, Guid secrecyLevelId, string title, DateTime completedTime, string storageNumber, DateTime incommingDate, string? no = null, string? currentStatus = null, DocumentSourceType sourceType = DocumentSourceType.Archive)
    {
        Id = id;
        Check.NotNull(title, nameof(title));
        Check.NotNull(storageNumber, nameof(storageNumber));
        Check.Length(storageNumber, nameof(storageNumber), DocumentConsts.StorageNumberMaxLength, 0);
        Check.Length(no, nameof(no), DocumentConsts.NoMaxLength, 0);
        Check.Length(currentStatus, nameof(currentStatus), DocumentConsts.CurrentStatusMaxLength, 0);
        Title = title;
        CompletedTime = completedTime;
        StorageNumber = storageNumber;
        IncommingDate = incommingDate;
        No = no;
        CurrentStatus = currentStatus;
        FieldId = fieldId;
        UnitId = unitId;
        WorkflowId = workflowId;
        StatusId = statusId;
        TypeId = typeId;
        UrgencyLevelId = urgencyLevelId;
        SecrecyLevelId = secrecyLevelId;
        SourceType = sourceType;
    }
}