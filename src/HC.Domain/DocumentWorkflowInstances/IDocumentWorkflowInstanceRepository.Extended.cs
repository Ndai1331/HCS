using System;
using System.Threading;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public partial interface IDocumentWorkflowInstanceRepository
{
    /// <summary>
    /// Batch-update IN_PROGRESS instances past deadline to OVERDUE (uses EF ExecuteUpdate).
    /// </summary>
    Task<int> MarkInProgressAsOverdueBatchAsync(DateTime now, CancellationToken cancellationToken = default);
}