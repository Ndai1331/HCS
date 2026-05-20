using Volo.Abp.Identity;
using HC.WorkflowStepTemplates;
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

namespace HC.WorkflowStepAssignments;

public abstract class EfCoreWorkflowStepAssignmentRepositoryBase : EfCoreRepository<HCDbContext, WorkflowStepAssignment, Guid>
{
    public EfCoreWorkflowStepAssignmentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? stepId = null, Guid? defaultUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, stepId, defaultUserId);
        var ids = query.Select(x => x.WorkflowStepAssignment.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<WorkflowStepAssignmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.WorkflowStepAssignment.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<WorkflowStepAssignmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? stepId = null, Guid? defaultUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, stepId, defaultUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowStepAssignmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<WorkflowStepAssignmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from workflowStepAssignment in (await GetDbSetAsync())
               join step in (await GetDbContextAsync()).Set<WorkflowStepTemplate>() on workflowStepAssignment.StepId equals step.Id into workflowStepTemplates
               from step in workflowStepTemplates.DefaultIfEmpty()
               join defaultUser in (await GetDbContextAsync()).Set<IdentityUser>() on workflowStepAssignment.DefaultUserId equals defaultUser.Id into identityUsers
               from defaultUser in identityUsers.DefaultIfEmpty()
               join role in (await GetDbContextAsync()).Set<IdentityRole>() on workflowStepAssignment.RoleId equals role.Id into identityRoles
               from role in identityRoles.DefaultIfEmpty()
               select new WorkflowStepAssignmentWithNavigationProperties
               {
                   WorkflowStepAssignment = workflowStepAssignment,
                   Step = step,
                   DefaultUser = defaultUser,
                   Role = role
               };
    }

    protected virtual IQueryable<WorkflowStepAssignmentWithNavigationProperties> ApplyFilter(IQueryable<WorkflowStepAssignmentWithNavigationProperties> query, string? filterText, bool? isPrimary = null, bool? isActive = null, Guid? stepId = null, Guid? defaultUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(isPrimary.HasValue, e => e.WorkflowStepAssignment.IsPrimary == isPrimary).WhereIf(isActive.HasValue, e => e.WorkflowStepAssignment.IsActive == isActive).WhereIf(stepId != null && stepId != Guid.Empty, e => e.Step != null && e.Step.Id == stepId).WhereIf(defaultUserId != null && defaultUserId != Guid.Empty, e => e.DefaultUser != null && e.DefaultUser.Id == defaultUserId);
    }

    public virtual async Task<List<WorkflowStepAssignment>> GetListAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, isPrimary, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? WorkflowStepAssignmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? stepId = null, Guid? defaultUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, stepId, defaultUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<WorkflowStepAssignment> ApplyFilter(IQueryable<WorkflowStepAssignment> query, string? filterText = null, bool? isPrimary = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(isPrimary.HasValue, e => e.IsPrimary == isPrimary).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}