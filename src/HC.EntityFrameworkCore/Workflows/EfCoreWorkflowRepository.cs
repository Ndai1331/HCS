using HC.WorkflowDefinitions;
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

namespace HC.Workflows;

public abstract class EfCoreWorkflowRepositoryBase : EfCoreRepository<HCDbContext, Workflow, Guid>
{
    public EfCoreWorkflowRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, Guid? workflowDefinitionId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, description, isActive, workflowDefinitionId);
        var ids = query.Select(x => x.Workflow.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<WorkflowWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.Workflow.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<WorkflowWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, Guid? workflowDefinitionId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, description, isActive, workflowDefinitionId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<WorkflowWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from workflow in (await GetDbSetAsync())
               join workflowDefinition in (await GetDbContextAsync()).Set<WorkflowDefinition>() on workflow.WorkflowDefinitionId equals workflowDefinition.Id into workflowDefinitions
               from workflowDefinition in workflowDefinitions.DefaultIfEmpty()
               select new WorkflowWithNavigationProperties
               {
                   Workflow = workflow,
                   WorkflowDefinition = workflowDefinition
               };
    }

    protected virtual IQueryable<WorkflowWithNavigationProperties> ApplyFilter(IQueryable<WorkflowWithNavigationProperties> query, string? filterText, string? code = null, string? name = null, string? description = null, bool? isActive = null, Guid? workflowDefinitionId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Workflow.Code!.Contains(filterText!) || e.Workflow.Name!.Contains(filterText!) || e.Workflow.Description!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Workflow.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Workflow.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(description), e => e.Workflow.Description.Contains(description)).WhereIf(isActive.HasValue, e => e.Workflow.IsActive == isActive).WhereIf(workflowDefinitionId != null && workflowDefinitionId != Guid.Empty, e => e.WorkflowDefinition != null && e.WorkflowDefinition.Id == workflowDefinitionId);
    }

    public virtual async Task<List<Workflow>> GetListAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, code, name, description, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null, Guid? workflowDefinitionId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, description, isActive, workflowDefinitionId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Workflow> ApplyFilter(IQueryable<Workflow> query, string? filterText = null, string? code = null, string? name = null, string? description = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Code!.Contains(filterText!) || e.Name!.Contains(filterText!) || e.Description!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(description), e => e.Description.Contains(description)).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}