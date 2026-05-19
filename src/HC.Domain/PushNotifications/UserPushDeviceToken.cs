using System;
using Volo.Abp.Domain.Entities.Auditing;
using Volo.Abp.MultiTenancy;

namespace HC.PushNotifications;

/// <summary>
/// Stores FCM registration tokens per user device for mobile push delivery.
/// </summary>
public class UserPushDeviceToken : AuditedAggregateRoot<Guid>, IMultiTenant
{
    public virtual Guid? TenantId { get; set; }

    public virtual Guid UserId { get; set; }

    public virtual string FcmToken { get; set; } = null!;

    public virtual string Platform { get; set; } = null!;

    public virtual string? DeviceId { get; set; }

    public virtual bool IsActive { get; set; } = true;

    public virtual DateTime LastSeenTime { get; set; }

    protected UserPushDeviceToken()
    {
    }

    public UserPushDeviceToken(
        Guid id,
        Guid userId,
        string fcmToken,
        string platform,
        string? deviceId,
        Guid? tenantId)
    {
        Id = id;
        UserId = userId;
        FcmToken = fcmToken;
        Platform = platform;
        DeviceId = deviceId;
        TenantId = tenantId;
        LastSeenTime = DateTime.UtcNow;
        IsActive = true;
    }

    public void UpdateToken(string fcmToken)
    {
        FcmToken = fcmToken;
        LastSeenTime = DateTime.UtcNow;
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
