using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Uow;

namespace HC.Notifications;

/// <summary>
/// Phase 3: processes <see cref="NotificationOutbox"/> rows (at-least-once delivery).
/// Extend ProcessOneAsync to deserialize PayloadJson and call email/in-app senders.
/// </summary>
public class NotificationOutboxBackgroundWorker : AsyncPeriodicBackgroundWorkerBase
{
    public NotificationOutboxBackgroundWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 5 * 60 * 1000; // 5 minutes
    }

    [UnitOfWork]
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        var repo = workerContext.ServiceProvider.GetRequiredService<IRepository<NotificationOutbox, Guid>>();
        var pendingCount = await repo.CountAsync(x => x.ProcessedTime == null);

        if (pendingCount == 0)
        {
            return;
        }

        Logger.LogWarning(
            "[OUTBOX] {Count} pending outbox row(s) — delivery not wired yet. " +
            "When ready: deserialize PayloadJson by EventType, send, then set ProcessedTime.",
            pendingCount);
    }
}
