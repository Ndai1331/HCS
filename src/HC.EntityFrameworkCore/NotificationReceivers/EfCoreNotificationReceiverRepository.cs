using Volo.Abp.Identity;
using HC.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Volo.Abp.Logging;
namespace HC.NotificationReceivers;

public abstract class EfCoreNotificationReceiverRepositoryBase : EfCoreRepository<HCDbContext, NotificationReceiver, Guid>
{
    private readonly ILogger<EfCoreNotificationReceiverRepositoryBase> _logger;
    public EfCoreNotificationReceiverRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreNotificationReceiverRepositoryBase> logger) : base(dbContextProvider)
    {
        _logger = logger;
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isRead, readAtMin, readAtMax, notificationId, identityUserId, null, creationTimeMin, creationTimeMax);
        var ids = query.Select(x => x.NotificationReceiver.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<NotificationReceiverWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(notificationReceiver => new NotificationReceiverWithNavigationProperties { NotificationReceiver = notificationReceiver, Notification = dbContext.Set<Notification>().FirstOrDefault(c => c.Id == notificationReceiver.NotificationId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == notificationReceiver.IdentityUserId) }).FirstOrDefault();
    }

    public virtual async Task<List<NotificationReceiverWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, string? sourceType = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isRead, readAtMin, readAtMax, notificationId, identityUserId, sourceType, creationTimeMin, creationTimeMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? NotificationReceiverConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<NotificationReceiverWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from notificationReceiver in (await GetDbSetAsync())
               join notification in (await GetDbContextAsync()).Set<Notification>() on notificationReceiver.NotificationId equals notification.Id into notifications
               from notification in notifications.DefaultIfEmpty()
               join identityUser in (await GetDbContextAsync()).Set<IdentityUser>() on notificationReceiver.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new NotificationReceiverWithNavigationProperties
               {
                   NotificationReceiver = notificationReceiver,
                   Notification = notification,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<NotificationReceiverWithNavigationProperties> ApplyFilter(IQueryable<NotificationReceiverWithNavigationProperties> query, string? filterText, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, string? sourceType = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null)
    {
        _logger.LogInformation($"CreationTimeMin: {creationTimeMin}");
        _logger.LogInformation($"CreationTimeMax: {creationTimeMax}");
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)
        .WhereIf(isRead.HasValue, e => e.NotificationReceiver.IsRead == isRead)
        .WhereIf(readAtMin.HasValue, e => e.NotificationReceiver.ReadAt >= readAtMin!.Value)
        .WhereIf(readAtMax.HasValue, e => e.NotificationReceiver.ReadAt <= readAtMax!.Value)
        .WhereIf(notificationId != null && notificationId != Guid.Empty, e => e.Notification != null && e.Notification.Id == notificationId)
        .WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId)
        .WhereIf(!string.IsNullOrWhiteSpace(sourceType), e => e.Notification != null && e.Notification.SourceType == sourceType)
        .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Notification != null && e.Notification.Title.Contains(filterText) || e.Notification != null && e.Notification.Content.Contains(filterText) || e.Notification != null && e.Notification.RelatedId.Contains(filterText))
        .WhereIf(creationTimeMin.HasValue, e => e.NotificationReceiver.CreationTime >= creationTimeMin!.Value)
        .WhereIf(creationTimeMax.HasValue, e => e.NotificationReceiver.CreationTime <= creationTimeMax!.Value);
    }

    public virtual async Task<List<NotificationReceiver>> GetListAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0,  CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, isRead, readAtMin, readAtMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? NotificationReceiverConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, string? sourceType = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isRead, readAtMin, readAtMax, notificationId, identityUserId, sourceType, creationTimeMin, creationTimeMax);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<NotificationReceiver> ApplyFilter(IQueryable<NotificationReceiver> query, string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(isRead.HasValue, e => e.IsRead == isRead).WhereIf(readAtMin.HasValue, e => e.ReadAt >= readAtMin!.Value).WhereIf(readAtMax.HasValue, e => e.ReadAt <= readAtMax!.Value);
    }
}