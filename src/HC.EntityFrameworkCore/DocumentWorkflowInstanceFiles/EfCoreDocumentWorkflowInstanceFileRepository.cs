using HC.DocumentFiles;
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

namespace HC.DocumentWorkflowInstanceFiles;

public abstract class EfCoreDocumentWorkflowInstanceFileRepositoryBase : EfCoreRepository<HCDbContext, DocumentWorkflowInstanceFile, Guid>
{
    public EfCoreDocumentWorkflowInstanceFileRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<List<DocumentWorkflowInstanceFile>> GetListByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = (await GetQueryableAsync()).Where(x => x.DocumentWorkflowInstanceId == documentWorkflowInstanceId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceFileConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync()).Where(x => x.DocumentWorkflowInstanceId == documentWorkflowInstanceId).CountAsync(cancellationToken);
    }

    public virtual async Task<List<DocumentWorkflowInstanceFileWithNavigationProperties>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = query.Where(x => x.DocumentWorkflowInstanceFile.DocumentWorkflowInstanceId == documentWorkflowInstanceId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceFileConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<DocumentWorkflowInstanceFileWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(documentWorkflowInstanceFile => new DocumentWorkflowInstanceFileWithNavigationProperties { DocumentWorkflowInstanceFile = documentWorkflowInstanceFile, DocumentFile = dbContext.Set<DocumentFile>().FirstOrDefault(c => c.Id == documentWorkflowInstanceFile.DocumentFileId) }).FirstOrDefault();
    }

    public virtual async Task<List<DocumentWorkflowInstanceFileWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? documentFileId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, documentFileId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceFileConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentWorkflowInstanceFileWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from documentWorkflowInstanceFile in (await GetDbSetAsync())
               join documentFile in (await GetDbContextAsync()).Set<DocumentFile>() on documentWorkflowInstanceFile.DocumentFileId equals documentFile.Id into documentFiles
               from documentFile in documentFiles.DefaultIfEmpty()
               select new DocumentWorkflowInstanceFileWithNavigationProperties
               {
                   DocumentWorkflowInstanceFile = documentWorkflowInstanceFile,
                   DocumentFile = documentFile
               };
    }

    protected virtual IQueryable<DocumentWorkflowInstanceFileWithNavigationProperties> ApplyFilter(IQueryable<DocumentWorkflowInstanceFileWithNavigationProperties> query, string? filterText, Guid? documentFileId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(documentFileId != null && documentFileId != Guid.Empty, e => e.DocumentFile != null && e.DocumentFile.Id == documentFileId);
    }

    public virtual async Task<List<DocumentWorkflowInstanceFile>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceFileConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, Guid? documentFileId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, documentFileId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentWorkflowInstanceFile> ApplyFilter(IQueryable<DocumentWorkflowInstanceFile> query, string? filterText = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true);
    }
}