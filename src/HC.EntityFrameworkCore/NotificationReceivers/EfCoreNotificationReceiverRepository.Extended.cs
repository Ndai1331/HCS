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

public class EfCoreNotificationReceiverRepository : EfCoreNotificationReceiverRepositoryBase, INotificationReceiverRepository
{
    private readonly ILogger<EfCoreNotificationReceiverRepository> _logger;
    public EfCoreNotificationReceiverRepository(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreNotificationReceiverRepository> logger) : base(dbContextProvider, logger)
    {
        _logger = logger;
    }

    public virtual async Task<List<NotificationReceiverWithNavigationProperties>> GetNotificationsByUserAndReadStatusAsync(
        Guid userId,
        bool isRead,
        string? filterText = null,
        string? sorting = null,
        int maxResultCount = int.MaxValue,
        int skipCount = 0,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = query.Where(x => x.NotificationReceiver.IdentityUserId == userId && x.NotificationReceiver.IsRead == isRead);

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            query = query.Where(x =>
                x.Notification.Title.Contains(filterText) ||
                x.Notification.Content.Contains(filterText));
        }

        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? "NotificationReceiver.CreationTime DESC" : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountByUserAndReadStatusAsync(
        Guid userId,
        bool isRead,
        string? filterText = null,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = query.Where(x => x.NotificationReceiver.IdentityUserId == userId && x.NotificationReceiver.IsRead == isRead);

        if (!string.IsNullOrWhiteSpace(filterText))
        {
            query = query.Where(x =>
                x.Notification.Title.Contains(filterText) ||
                x.Notification.Content.Contains(filterText));
        }

        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }
}