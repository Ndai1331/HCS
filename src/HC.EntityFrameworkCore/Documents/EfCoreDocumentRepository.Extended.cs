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

namespace HC.Documents;

public class EfCoreDocumentRepository : EfCoreDocumentRepositoryBase, IDocumentRepository
{
    public EfCoreDocumentRepository(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    /// <summary>
    /// M8: ILIKE-based duplicate check. An exact (non-wildcard) ILIKE on a column with a
    /// trigram GIN index is still indexable by PostgreSQL and preserves case-insensitivity.
    /// </summary>
    public virtual async Task<bool> AnyByNoAsync(string no, Guid? excludeDocumentId = null, CancellationToken cancellationToken = default)
    {
        var normalized = no?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var query = (await GetQueryableAsync())
            .Where(x => x.No != null && EF.Functions.ILike(x.No!, normalized!));

        if (excludeDocumentId.HasValue && excludeDocumentId.Value != Guid.Empty)
        {
            var id = excludeDocumentId.Value;
            query = query.Where(x => x.Id != id);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<bool> AnyByStorageNumberAsync(string storageNumber, Guid? excludeDocumentId = null, CancellationToken cancellationToken = default)
    {
        var normalized = storageNumber?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        var query = (await GetQueryableAsync())
            .Where(x => x.StorageNumber != null && EF.Functions.ILike(x.StorageNumber!, normalized!));

        if (excludeDocumentId.HasValue && excludeDocumentId.Value != Guid.Empty)
        {
            var id = excludeDocumentId.Value;
            query = query.Where(x => x.Id != id);
        }

        return await query.AnyAsync(GetCancellationToken(cancellationToken));
    }
}