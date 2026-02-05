using Volo.Abp.Identity;
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
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace HC.DocumentHistories;

public abstract class EfCoreDocumentHistoryRepositoryBase : EfCoreRepository<HCDbContext, DocumentHistory, Guid>
{
    protected ILogger<EfCoreDocumentHistoryRepositoryBase> Logger;
    public EfCoreDocumentHistoryRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreDocumentHistoryRepositoryBase> logger) : base(dbContextProvider)
    {
        Logger = logger;
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, comment, action, documentId, fromUser, toUser);
        var ids = query.Select(x => x.DocumentHistory.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DocumentHistoryWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(documentHistory => new DocumentHistoryWithNavigationProperties { DocumentHistory = documentHistory, Document = dbContext.Set<Document>().FirstOrDefault(c => c.Id == documentHistory.DocumentId), FromUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == documentHistory.FromUser), ToUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == documentHistory.ToUser) }).FirstOrDefault();
    }

    public virtual async Task<List<DocumentHistoryWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        try{   
            var query = await GetQueryForNavigationPropertiesAsync();
            query = ApplyFilter(query, filterText, comment, action, documentId, fromUser, toUser);
            query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentHistoryConsts.GetDefaultSorting(true) : sorting);
            Logger.LogInformation($"GetListWithNavigationPropertiesAsync query: {JsonSerializer.Serialize(query)}");
            var res = await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
            Logger.LogInformation($"GetListWithNavigationPropertiesAsync returned {JsonSerializer.Serialize(res)} items");
            return res;
        }catch(Exception ex){
            Logger.LogError($"GetListWithNavigationPropertiesAsync error: {ex}");
            return new List<DocumentHistoryWithNavigationProperties>();
        }
    }

    protected virtual async Task<IQueryable<DocumentHistoryWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
            try{
            Logger.LogInformation($"GetQueryForNavigationPropertiesAsync called");
            var query = from documentHistory in (await GetDbSetAsync())
                join document in (await GetDbContextAsync()).Set<Document>() on documentHistory.DocumentId equals document.Id into documents
                from document in documents.DefaultIfEmpty()
                join fromUser in (await GetDbContextAsync()).Set<IdentityUser>() on documentHistory.FromUser equals fromUser.Id into identityUsers
                from fromUser in identityUsers.DefaultIfEmpty()
                join toUser in (await GetDbContextAsync()).Set<IdentityUser>() on documentHistory.ToUser equals toUser.Id into identityUsers1
                from toUser in identityUsers1.DefaultIfEmpty()
                select new DocumentHistoryWithNavigationProperties
                {
                    DocumentHistory = documentHistory,
                    Document = document,
                    FromUser = fromUser,
                    ToUser = toUser
                };
            Logger.LogInformation($"GetQueryForNavigationPropertiesAsync returned {JsonSerializer.Serialize(query)}");
            return query;
        }catch(Exception ex){
            Logger.LogError($"GetQueryForNavigationPropertiesAsync error: {ex}");
            throw;
        }
    }

    protected virtual IQueryable<DocumentHistoryWithNavigationProperties> ApplyFilter(IQueryable<DocumentHistoryWithNavigationProperties> query, string? filterText, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null)
    {
        Logger.LogInformation($"ApplyFilter called with filterText: {filterText}, comment: {comment}, action: {action}, documentId: {documentId}, fromUser: {fromUser}, toUser: {toUser}");
        return query
        .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => 
        e.DocumentHistory.Comment!.Contains(filterText!) || e.DocumentHistory.Action!.Contains(filterText!))
        .WhereIf(!string.IsNullOrWhiteSpace(comment), e => e.DocumentHistory.Comment.Contains(comment))
        .WhereIf(!string.IsNullOrWhiteSpace(action), e => e.DocumentHistory.Action.Contains(action))
        .WhereIf(documentId != null && documentId != Guid.Empty, e => e.Document != null && e.Document.Id == documentId)
        .WhereIf(fromUser != null && fromUser != Guid.Empty, e => e.FromUser != null && e.FromUser.Id == fromUser)
        .WhereIf(toUser != null && toUser != Guid.Empty, e => e.ToUser != null && e.ToUser.Id == toUser);
    }

    public virtual async Task<List<DocumentHistory>> GetListAsync(string? filterText = null, string? comment = null, string? action = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, comment, action);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentHistoryConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, comment, action, documentId, fromUser, toUser);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentHistory> ApplyFilter(IQueryable<DocumentHistory> query, string? filterText = null, string? comment = null, string? action = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Comment!.Contains(filterText!) || e.Action!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(comment), e => e.Comment.Contains(comment)).WhereIf(!string.IsNullOrWhiteSpace(action), e => e.Action.Contains(action));
    }

    public virtual async Task<List<DocumentHistoryWithNavigationProperties>> GetHistoryByDocumentIdAsync(
        Guid documentId,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        
        // Filter by document ID
        query = query.Where(x => x.DocumentHistory.DocumentId == documentId);
        
        // Sort by CreationTime DESC (newest first) - using DocumentHistory.CreationTime
        query = query.OrderByDescending(x => x.DocumentHistory.CreationTime);
        
        // Apply pagination
        return await query
            .Skip(skipCount)
            .Take(maxResultCount)
            .ToListAsync(cancellationToken);
    }
    
    public virtual async Task<long> GetCountByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        
        // Filter by document ID
        query = query.Where(x => x.DocumentHistory.DocumentId == documentId);
        
        return await query.LongCountAsync(cancellationToken);
    }
}

public class EfCoreDocumentHistoryRepository : EfCoreDocumentHistoryRepositoryBase, IDocumentHistoryRepository
{
    public EfCoreDocumentHistoryRepository(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreDocumentHistoryRepositoryBase> logger)
        : base(dbContextProvider, logger)
    {
    }
}