using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HC.Documents;

/// <summary>
/// Tracks long-running document operations (approve-with-note, future: digital sign) for 202 + progress UI.
/// </summary>
public class DocumentBackgroundOperation : FullAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>User who initiated the operation (receives SignalR progress).</summary>
    public virtual Guid UserId { get; set; }

    public virtual Guid? DocumentId { get; set; }

    /// <summary>See <see cref="DocumentBackgroundOperationTypes"/>.</summary>
    public virtual string OperationType { get; set; } = null!;

    /// <summary>See <see cref="DocumentBackgroundOperationStatuses"/>.</summary>
    public virtual string Status { get; set; } = DocumentBackgroundOperationStatuses.Pending;

    /// <summary>0–100.</summary>
    public virtual int Progress { get; set; }

    public virtual string? Message { get; set; }

    public virtual string? ErrorMessage { get; set; }

    /// <summary>Serialized input (e.g. JSON of <c>ApproveDocumentWithNoteInput</c>).</summary>
    public virtual string? InputJson { get; set; }

    protected DocumentBackgroundOperation()
    {
    }

    public DocumentBackgroundOperation(
        Guid id,
        Guid userId,
        Guid? documentId,
        string operationType,
        string? inputJson,
        Guid? tenantId)
    {
        Id = id;
        UserId = userId;
        DocumentId = documentId;
        OperationType = operationType;
        InputJson = inputJson;
        TenantId = tenantId;
    }
}
