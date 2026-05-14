using HC.Documents;
using HC.ProjectTasks;
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

namespace HC.ProjectTaskDocuments;

public abstract class EfCoreProjectTaskDocumentRepositoryBase : EfCoreRepository<HCDbContext, ProjectTaskDocument, Guid>
{
    public EfCoreProjectTaskDocumentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? documentPurpose = null, Guid? projectTaskId = null, Guid? documentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, documentPurpose, projectTaskId, documentId);
        var ids = query.Select(x => x.ProjectTaskDocument.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ProjectTaskDocumentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.ProjectTaskDocument.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ProjectTaskDocumentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? documentPurpose = null, Guid? projectTaskId = null, Guid? documentId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, documentPurpose, projectTaskId, documentId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskDocumentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ProjectTaskDocumentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from projectTaskDocument in (await GetDbSetAsync())
               join projectTask in (await GetDbContextAsync()).Set<ProjectTask>() on projectTaskDocument.ProjectTaskId equals projectTask.Id into projectTasks
               from projectTask in projectTasks.DefaultIfEmpty()
               join document in (await GetDbContextAsync()).Set<Document>() on projectTaskDocument.DocumentId equals document.Id into documents
               from document in documents.DefaultIfEmpty()
               select new ProjectTaskDocumentWithNavigationProperties
               {
                   ProjectTaskDocument = projectTaskDocument,
                   ProjectTask = projectTask,
                   Document = document
               };
    }

    protected virtual IQueryable<ProjectTaskDocumentWithNavigationProperties> ApplyFilter(IQueryable<ProjectTaskDocumentWithNavigationProperties> query, string? filterText, string? documentPurpose = null, Guid? projectTaskId = null, Guid? documentId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ProjectTaskDocument.DocumentPurpose!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(documentPurpose), e => e.ProjectTaskDocument.DocumentPurpose.Contains(documentPurpose)).WhereIf(projectTaskId != null && projectTaskId != Guid.Empty, e => e.ProjectTask != null && e.ProjectTask.Id == projectTaskId).WhereIf(documentId != null && documentId != Guid.Empty, e => e.Document != null && e.Document.Id == documentId);
    }

    public virtual async Task<List<ProjectTaskDocument>> GetListAsync(string? filterText = null, string? documentPurpose = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, documentPurpose);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskDocumentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? documentPurpose = null, Guid? projectTaskId = null, Guid? documentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, documentPurpose, projectTaskId, documentId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<ProjectTaskDocument> ApplyFilter(IQueryable<ProjectTaskDocument> query, string? filterText = null, string? documentPurpose = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DocumentPurpose!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(documentPurpose), e => e.DocumentPurpose.Contains(documentPurpose));
    }

    public virtual async Task<Dictionary<Guid, int>> GetCountByProjectTaskIdsAsync(
        List<Guid> projectTaskIds,
        CancellationToken cancellationToken = default)
    {
        // Count documents per task in one query.
        if (projectTaskIds == null || projectTaskIds.Count == 0)
        {
            return new Dictionary<Guid, int>();
        }

        return await (await GetDbSetAsync())
            .Where(x => projectTaskIds.Contains(x.ProjectTaskId))
            .GroupBy(x => x.ProjectTaskId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, GetCancellationToken(cancellationToken));
    }
}