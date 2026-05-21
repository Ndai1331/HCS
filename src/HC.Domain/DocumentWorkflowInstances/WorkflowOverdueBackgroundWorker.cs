using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HC.DocumentWorkflowInstances;

public class WorkflowOverdueBackgroundWorker : AsyncPeriodicBackgroundWorkerBase
{
    public WorkflowOverdueBackgroundWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        Timer.Period = 3 * 60 * 1000;
    }

    [UnitOfWork]
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("[OVERDUE_WORKER] Starting overdue workflow check...");

        var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();
        var instanceRepository = workerContext.ServiceProvider.GetRequiredService<IDocumentWorkflowInstanceRepository>();
        var cancellationService = workerContext.ServiceProvider.GetRequiredService<WorkflowOverdueCancellationService>();

        var now = clock.Now;

        var markOverdueCandidates = await instanceRepository.GetListAsync(
            x => x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
                 && x.FinishedAt > DateTime.MinValue
                 && x.FinishedAt <= now);

        foreach (var instance in markOverdueCandidates)
        {
            try
            {
                instance.Status = nameof(DocumentWorkflowInstanceStatus.OVERDUE);
                instance.OverdueAt = now;
                await instanceRepository.UpdateAsync(instance);
                Logger.LogInformation(
                    "[OVERDUE_WORKER] Marked instance {InstanceId} as OVERDUE at {OverdueAt}",
                    instance.Id, now);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[OVERDUE_WORKER] Failed to mark OVERDUE for instance {InstanceId}", instance.Id);
            }
        }

        var overdueInstances = await instanceRepository.GetListAsync(
            x => x.Status == nameof(DocumentWorkflowInstanceStatus.OVERDUE)
                 && x.OverdueAt.HasValue);

        var cancelCount = 0;
        foreach (var instance in overdueInstances)
        {
            var graceCancelAt = BusinessDayCalculator.GetOverdueGraceCancelAt(instance.OverdueAt!.Value);
            if (now < graceCancelAt)
            {
                continue;
            }

            try
            {
                await cancellationService.CancelOverdueInstanceAsync(instance, now, Logger);
                cancelCount++;
                Logger.LogInformation(
                    "[OVERDUE_WORKER] Cancelled instance {InstanceId} after grace (OverdueAt={OverdueAt}, GraceEnd={GraceEnd})",
                    instance.Id, instance.OverdueAt, graceCancelAt);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[OVERDUE_WORKER] Failed to cancel instance {InstanceId}", instance.Id);
            }
        }

        Logger.LogInformation(
            "[OVERDUE_WORKER] Completed. Marked {Marked} overdue, cancelled {Cancelled} after grace.",
            markOverdueCandidates.Count, cancelCount);
    }
}
