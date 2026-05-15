using HC.WorkflowTemplates;
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

namespace HC.WorkflowStepTemplates;

public abstract class EfCoreWorkflowStepTemplateRepositoryBase : EfCoreRepository<HCDbContext, WorkflowStepTemplate, Guid>
{
    public EfCoreWorkflowStepTemplateRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null, Guid? workflowTemplateId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, orderMin, orderMax, name, type, sLADaysMin, sLADaysMax, isActive, workflowTemplateId);
        var ids = query.Select(x => x.WorkflowStepTemplate.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<WorkflowStepTemplateWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.WorkflowStepTemplate.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<WorkflowStepTemplateWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null, Guid? workflowTemplateId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, orderMin, orderMax, name, type, sLADaysMin, sLADaysMax, isActive, workflowTemplateId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowStepTemplateConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<WorkflowStepTemplateWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from workflowStepTemplate in (await GetDbSetAsync())
               join workflowTemplate in (await GetDbContextAsync()).Set<WorkflowTemplate>() on workflowStepTemplate.WorkflowTemplateId equals workflowTemplate.Id into workflowTemplates
               from workflowTemplate in workflowTemplates.DefaultIfEmpty()
               select new WorkflowStepTemplateWithNavigationProperties
               {
                   WorkflowStepTemplate = workflowStepTemplate,
                   WorkflowTemplate = workflowTemplate
               };
    }

    protected virtual IQueryable<WorkflowStepTemplateWithNavigationProperties> ApplyFilter(IQueryable<WorkflowStepTemplateWithNavigationProperties> query, string? filterText, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null, Guid? workflowTemplateId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.WorkflowStepTemplate.Name!.Contains(filterText!) || e.WorkflowStepTemplate.Type!.Contains(filterText!)).WhereIf(orderMin.HasValue, e => e.WorkflowStepTemplate.Order >= orderMin!.Value).WhereIf(orderMax.HasValue, e => e.WorkflowStepTemplate.Order <= orderMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.WorkflowStepTemplate.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(type), e => e.WorkflowStepTemplate.Type.Contains(type)).WhereIf(sLADaysMin.HasValue, e => e.WorkflowStepTemplate.SLADays >= sLADaysMin!.Value).WhereIf(sLADaysMax.HasValue, e => e.WorkflowStepTemplate.SLADays <= sLADaysMax!.Value).WhereIf(isActive.HasValue, e => e.WorkflowStepTemplate.IsActive == isActive).WhereIf(workflowTemplateId != null && workflowTemplateId != Guid.Empty, e => e.WorkflowTemplate != null && e.WorkflowTemplate.Id == workflowTemplateId);
    }

    public virtual async Task<List<WorkflowStepTemplate>> GetListAsync(string? filterText = null, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, orderMin, orderMax, name, type, sLADaysMin, sLADaysMax, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowStepTemplateConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null, Guid? workflowTemplateId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, orderMin, orderMax, name, type, sLADaysMin, sLADaysMax, isActive, workflowTemplateId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<WorkflowStepTemplate> ApplyFilter(IQueryable<WorkflowStepTemplate> query, string? filterText = null, int? orderMin = null, int? orderMax = null, string? name = null, string? type = null, int? sLADaysMin = null, int? sLADaysMax = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Name!.Contains(filterText!) || e.Type!.Contains(filterText!)).WhereIf(orderMin.HasValue, e => e.Order >= orderMin!.Value).WhereIf(orderMax.HasValue, e => e.Order <= orderMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(type), e => e.Type.Contains(type)).WhereIf(sLADaysMin.HasValue, e => e.SLADays >= sLADaysMin!.Value).WhereIf(sLADaysMax.HasValue, e => e.SLADays <= sLADaysMax!.Value).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}