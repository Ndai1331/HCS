using System;
using System.Threading;
using System.Threading.Tasks;

namespace HC.Documents;

public partial interface IDocumentRepository
{
    /// <summary>
    /// M8: case-insensitive duplicate check on the Documents.No column.
    /// Runs inside the EF layer so we can call <c>EF.Functions.ILike</c> (which PostgreSQL
    /// can serve from the pg_trgm GIN index on <c>No</c>).
    /// </summary>
    Task<bool> AnyByNoAsync(string no, Guid? excludeDocumentId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// M8: case-insensitive duplicate check on the Documents.StorageNumber column.
    /// Uses the same pg_trgm-backed ILIKE strategy as <see cref="AnyByNoAsync"/>.
    /// </summary>
    Task<bool> AnyByStorageNumberAsync(string storageNumber, Guid? excludeDocumentId = null, CancellationToken cancellationToken = default);
}
