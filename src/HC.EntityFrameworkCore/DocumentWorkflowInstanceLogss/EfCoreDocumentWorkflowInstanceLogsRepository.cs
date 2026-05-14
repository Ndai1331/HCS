using Volo.Abp.Identity;
using HC.DocumentAssignments;
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

namespace HC.DocumentWorkflowInstanceLogss;

public abstract class EfCoreDocumentWorkflowInstanceLogsRepositoryBase : EfCoreRepository<HCDbContext, DocumentWorkflowInstanceLogs, Guid>
{
    public EfCoreDocumentWorkflowInstanceLogsRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<List<DocumentWorkflowInstanceLogs>> GetListByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = (await GetQueryableAsync()).Where(x => x.DocumentWorkflowInstanceId == documentWorkflowInstanceId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceLogsConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, CancellationToken cancellationToken = default)
    {
        return await (await GetQueryableAsync()).Where(x => x.DocumentWorkflowInstanceId == documentWorkflowInstanceId).CountAsync(cancellationToken);
    }

    public virtual async Task<List<DocumentWorkflowInstanceLogsWithNavigationProperties>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = query.Where(x => x.DocumentWorkflowInstanceLogs.DocumentWorkflowInstanceId == documentWorkflowInstanceId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceLogsConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<DocumentWorkflowInstanceLogsWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.DocumentWorkflowInstanceLogs.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DocumentWorkflowInstanceLogsWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, Guid? documentAssignmentId = null, Guid? actorUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, action, actorRole, fromStatus, toStatus, note, documentAssignmentId, actorUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceLogsConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DocumentWorkflowInstanceLogsWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from documentWorkflowInstanceLogs in (await GetDbSetAsync())
               join documentAssignment in (await GetDbContextAsync()).Set<DocumentAssignment>() on documentWorkflowInstanceLogs.DocumentAssignmentId equals documentAssignment.Id into documentAssignments
               from documentAssignment in documentAssignments.DefaultIfEmpty()
               join actorUser in (await GetDbContextAsync()).Set<IdentityUser>() on documentWorkflowInstanceLogs.ActorUserId equals actorUser.Id into identityUsers
               from actorUser in identityUsers.DefaultIfEmpty()
               select new DocumentWorkflowInstanceLogsWithNavigationProperties
               {
                   DocumentWorkflowInstanceLogs = documentWorkflowInstanceLogs,
                   DocumentAssignment = documentAssignment,
                   ActorUser = actorUser
               };
    }

    protected virtual IQueryable<DocumentWorkflowInstanceLogsWithNavigationProperties> ApplyFilter(IQueryable<DocumentWorkflowInstanceLogsWithNavigationProperties> query, string? filterText, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, Guid? documentAssignmentId = null, Guid? actorUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.DocumentWorkflowInstanceLogs.Action!.Contains(filterText!) || e.DocumentWorkflowInstanceLogs.ActorRole!.Contains(filterText!) || e.DocumentWorkflowInstanceLogs.FromStatus!.Contains(filterText!) || e.DocumentWorkflowInstanceLogs.ToStatus!.Contains(filterText!) || e.DocumentWorkflowInstanceLogs.Note!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(action), e => e.DocumentWorkflowInstanceLogs.Action.Contains(action)).WhereIf(!string.IsNullOrWhiteSpace(actorRole), e => e.DocumentWorkflowInstanceLogs.ActorRole.Contains(actorRole)).WhereIf(!string.IsNullOrWhiteSpace(fromStatus), e => e.DocumentWorkflowInstanceLogs.FromStatus.Contains(fromStatus)).WhereIf(!string.IsNullOrWhiteSpace(toStatus), e => e.DocumentWorkflowInstanceLogs.ToStatus.Contains(toStatus)).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.DocumentWorkflowInstanceLogs.Note.Contains(note)).WhereIf(documentAssignmentId != null && documentAssignmentId != Guid.Empty, e => e.DocumentAssignment != null && e.DocumentAssignment.Id == documentAssignmentId).WhereIf(actorUserId != null && actorUserId != Guid.Empty, e => e.ActorUser != null && e.ActorUser.Id == actorUserId);
    }

    public virtual async Task<List<DocumentWorkflowInstanceLogs>> GetListAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, action, actorRole, fromStatus, toStatus, note);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DocumentWorkflowInstanceLogsConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, Guid? documentAssignmentId = null, Guid? actorUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, action, actorRole, fromStatus, toStatus, note, documentAssignmentId, actorUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<DocumentWorkflowInstanceLogs> ApplyFilter(IQueryable<DocumentWorkflowInstanceLogs> query, string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Action!.Contains(filterText!) || e.ActorRole!.Contains(filterText!) || e.FromStatus!.Contains(filterText!) || e.ToStatus!.Contains(filterText!) || e.Note!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(action), e => e.Action.Contains(action)).WhereIf(!string.IsNullOrWhiteSpace(actorRole), e => e.ActorRole.Contains(actorRole)).WhereIf(!string.IsNullOrWhiteSpace(fromStatus), e => e.FromStatus.Contains(fromStatus)).WhereIf(!string.IsNullOrWhiteSpace(toStatus), e => e.ToStatus.Contains(toStatus)).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.Note.Contains(note));
    }
}