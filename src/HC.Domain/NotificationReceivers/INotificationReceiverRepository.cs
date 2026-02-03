using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.NotificationReceivers;

public partial interface INotificationReceiverRepository : IRepository<NotificationReceiver, Guid>
{
    Task DeleteAllAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default);
    Task<NotificationReceiverWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<NotificationReceiverWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, string? sourceType = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default);
    Task<List<NotificationReceiver>> GetListAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, bool? isRead = null, DateTime? readAtMin = null, DateTime? readAtMax = null, Guid? notificationId = null, Guid? identityUserId = null, string? sourceType = null, DateTime? creationTimeMin = null, DateTime? creationTimeMax = null, CancellationToken cancellationToken = default);
}