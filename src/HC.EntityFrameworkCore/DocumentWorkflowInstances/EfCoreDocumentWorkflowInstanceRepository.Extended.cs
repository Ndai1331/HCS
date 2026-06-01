using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.DocumentWorkflowInstances;

public class EfCoreDocumentWorkflowInstanceRepository : EfCoreDocumentWorkflowInstanceRepositoryBase, IDocumentWorkflowInstanceRepository
{
    public EfCoreDocumentWorkflowInstanceRepository(IDbContextProvider<HCDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public virtual async Task<int> MarkInProgressAsOverdueBatchAsync(
        DateTime now,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        var inProgressStatus = nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS);
        var overdueStatus = nameof(DocumentWorkflowInstanceStatus.OVERDUE);

        return await dbSet
            .Where(x => x.Status == inProgressStatus
                        && x.FinishedAt > DateTime.MinValue
                        && x.FinishedAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(x => x.Status, overdueStatus)
                    .SetProperty(x => x.OverdueAt, now),
                cancellationToken);
    }
}
