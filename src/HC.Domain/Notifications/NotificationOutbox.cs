using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HC.Notifications;

/// <summary>
/// Transactional outbox for side-effects (email, in-app notify) written in the same UoW as business data.
/// A background worker delivers payloads asynchronously (at-least-once).
/// </summary>
public class NotificationOutbox : CreationAuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    /// <summary>Discriminator for the worker (e.g. NotificationCreated, DocumentApproved).</summary>
    public virtual string EventType { get; set; } = null!;

    public virtual string PayloadJson { get; set; } = null!;

    public virtual DateTime? ProcessedTime { get; set; }

    public virtual int RetryCount { get; set; }

    public virtual string? ErrorMessage { get; set; }

    protected NotificationOutbox()
    {
    }

    public NotificationOutbox(
        Guid id,
        string eventType,
        string payloadJson,
        Guid? tenantId)
    {
        Id = id;
        EventType = eventType;
        PayloadJson = payloadJson;
        TenantId = tenantId;
    }
}
