using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;

namespace HC.DocumentWorkflowInstances;

public class EfCoreDocumentWorkflowInstanceExtensionRepository
    : EfCoreRepository<HCDbContext, DocumentWorkflowInstanceExtension, Guid>,
        IDocumentWorkflowInstanceExtensionRepository
{
    public EfCoreDocumentWorkflowInstanceExtensionRepository(IDbContextProvider<HCDbContext> dbContextProvider)
        : base(dbContextProvider)
    {
    }

    public async Task<List<DocumentWorkflowInstanceExtension>> GetListByInstanceIdAsync(
        Guid workflowInstanceId,
        CancellationToken cancellationToken = default)
    {
        var dbSet = await GetDbSetAsync();
        return await dbSet
            .Where(x => x.DocumentWorkflowInstanceId == workflowInstanceId)
            .OrderByDescending(x => x.CreationTime)
            .ToListAsync(GetCancellationToken(cancellationToken));
    }
}
