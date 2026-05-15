using HC.Workflows;
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

namespace HC.WorkflowTemplates;

public abstract class EfCoreWorkflowTemplateRepositoryBase : EfCoreRepository<HCDbContext, WorkflowTemplate, Guid>
{
    public EfCoreWorkflowTemplateRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? outputFormat = null, Guid? workflowId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, outputFormat, workflowId);
        var ids = query.Select(x => x.WorkflowTemplate.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<WorkflowTemplateWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.WorkflowTemplate.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<WorkflowTemplateWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null, string? outputFormat = null, Guid? workflowId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, outputFormat, workflowId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowTemplateConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<WorkflowTemplateWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from workflowTemplate in (await GetDbSetAsync())
               join workflow in (await GetDbContextAsync()).Set<Workflow>() on workflowTemplate.WorkflowId equals workflow.Id into workflows
               from workflow in workflows.DefaultIfEmpty()
               select new WorkflowTemplateWithNavigationProperties
               {
                   WorkflowTemplate = workflowTemplate,
                   Workflow = workflow
               };
    }

    protected virtual IQueryable<WorkflowTemplateWithNavigationProperties> ApplyFilter(IQueryable<WorkflowTemplateWithNavigationProperties> query, string? filterText, string? code = null, string? name = null, string? outputFormat = null, Guid? workflowId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.WorkflowTemplate.Code!.Contains(filterText!) || e.WorkflowTemplate.Name!.Contains(filterText!) || e.WorkflowTemplate.OutputFormat!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.WorkflowTemplate.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.WorkflowTemplate.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(outputFormat), e => e.WorkflowTemplate.OutputFormat.Contains(outputFormat)).WhereIf(workflowId != null && workflowId != Guid.Empty, e => e.Workflow != null && e.Workflow.Id == workflowId);
    }

    public virtual async Task<List<WorkflowTemplate>> GetListAsync(string? filterText = null, string? code = null, string? name = null, string? outputFormat = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, code, name, outputFormat);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowTemplateConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? outputFormat = null, Guid? workflowId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, outputFormat, workflowId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<WorkflowTemplate> ApplyFilter(IQueryable<WorkflowTemplate> query, string? filterText = null, string? code = null, string? name = null, string? outputFormat = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Code!.Contains(filterText!) || e.Name!.Contains(filterText!) || e.OutputFormat!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(outputFormat), e => e.OutputFormat.Contains(outputFormat));
    }
}