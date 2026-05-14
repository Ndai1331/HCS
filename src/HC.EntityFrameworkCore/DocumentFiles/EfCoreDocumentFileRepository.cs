using HC.Documents;
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

namespace HC.DocumentFiles;

public abstract class EfCoreDocumentFileRepositoryBase : EfCoreRepository<HCDbContext, DocumentFile, Guid>
{
    public EfCoreDocumentFileRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null, Guid? documentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, path, hash, isSigned, uploadedAtMin, uploadedAtMax, documentId);
        var ids = query.Select(x => x.DocumentFile.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentFileWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.DocumentFile.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DocumentFileWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null, Guid? documentId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, path, hash, isSigned, uploadedAtMin, uploadedAtMax, documentId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentFileConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentFileWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from documentFile in (await GetDbSetAsync())
               join document in (await GetDbContextAsync()).Set<Document>() on documentFile.DocumentId equals document.Id into documents
               from document in documents.DefaultIfEmpty()
               select new DocumentFileWithNavigationProperties
               {
                   DocumentFile = documentFile,
                   Document = document
               };
    }

    protected virtual IQueryable<DocumentFileWithNavigationProperties> ApplyFilter(IQueryable<DocumentFileWithNavigationProperties> query, string? filterText, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null, Guid? documentId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DocumentFile.Name!.Contains(filterText!) || e.DocumentFile.Path!.Contains(filterText!) || e.DocumentFile.Hash!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.DocumentFile.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(path), e => e.DocumentFile.Path.Contains(path)).WhereIf(!string.IsNullOrWhiteSpace(hash), e => e.DocumentFile.Hash.Contains(hash)).WhereIf(isSigned.HasValue, e => e.DocumentFile.IsSigned == isSigned).WhereIf(uploadedAtMin.HasValue, e => e.DocumentFile.UploadedAt >= uploadedAtMin!.Value).WhereIf(uploadedAtMax.HasValue, e => e.DocumentFile.UploadedAt <= uploadedAtMax!.Value).WhereIf(documentId != null && documentId != Guid.Empty, e => e.Document != null && e.Document.Id == documentId);
    }

    public virtual async Task<List<DocumentFile>> GetListAsync(string? filterText = null, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, name, path, hash, isSigned, uploadedAtMin, uploadedAtMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentFileConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null, Guid? documentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, name, path, hash, isSigned, uploadedAtMin, uploadedAtMax, documentId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentFile> ApplyFilter(IQueryable<DocumentFile> query, string? filterText = null, string? name = null, string? path = null, string? hash = null, bool? isSigned = null, DateTime? uploadedAtMin = null, DateTime? uploadedAtMax = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!) || e.Path!.Contains(filterText!) || e.Hash!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(path), e => e.Path.Contains(path)).WhereIf(!string.IsNullOrWhiteSpace(hash), e => e.Hash.Contains(hash)).WhereIf(isSigned.HasValue, e => e.IsSigned == isSigned).WhereIf(uploadedAtMin.HasValue, e => e.UploadedAt >= uploadedAtMin!.Value).WhereIf(uploadedAtMax.HasValue, e => e.UploadedAt <= uploadedAtMax!.Value);
    }
}