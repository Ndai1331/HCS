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

namespace HC.WorkflowDefinitions;

public abstract class EfCoreWorkflowDefinitionRepositoryBase : EfCoreRepository<HCDbContext, WorkflowDefinition, Guid>
{
    public EfCoreWorkflowDefinitionRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryableAsync();
        query = ApplyFilter(query, filterText, code, name, description, isActive);
        var ids = query.Select(x => x.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<WorkflowDefinition>> GetListAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, code, name, description, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowDefinitionConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetDbSetAsync()), filterText, code, name, description, isActive);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<WorkflowDefinition> ApplyFilter(IQueryable<WorkflowDefinition> query, string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Code!.Contains(filterText!) || e.Name!.Contains(filterText!) || e.Description!.Contains(filterText!))
        .WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code))
        .WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name))
        .WhereIf(!string.IsNullOrWhiteSpace(description), e => e.Description.Contains(description))
        .WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}