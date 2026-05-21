using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstances;

public interface IDocumentWorkflowInstanceExtensionRepository : IRepository<DocumentWorkflowInstanceExtension, Guid>
{
    Task<List<DocumentWorkflowInstanceExtension>> GetListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default);
}
