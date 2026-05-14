using Volo.Abp.Identity;
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

namespace HC.ProjectTaskAssignments;

public abstract class EfCoreProjectTaskAssignmentRepositoryBase : EfCoreRepository<HCDbContext, ProjectTaskAssignment, Guid>
{
    public EfCoreProjectTaskAssignmentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null, Guid? projectTaskId = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, assignmentRole, assignedAtMin, assignedAtMax, note, projectTaskId, userId);
        var ids = query.Select(x => x.ProjectTaskAssignment.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ProjectTaskAssignmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.ProjectTaskAssignment.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<ProjectTaskAssignmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null, Guid? projectTaskId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, assignmentRole, assignedAtMin, assignedAtMax, note, projectTaskId, userId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskAssignmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ProjectTaskAssignmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from projectTaskAssignment in (await GetDbSetAsync())
               join projectTask in (await GetDbContextAsync()).Set<ProjectTask>() on projectTaskAssignment.ProjectTaskId equals projectTask.Id into projectTasks
               from projectTask in projectTasks.DefaultIfEmpty()
               join user in (await GetDbContextAsync()).Set<IdentityUser>() on projectTaskAssignment.UserId equals user.Id into identityUsers
               from user in identityUsers.DefaultIfEmpty()
               select new ProjectTaskAssignmentWithNavigationProperties
               {
                   ProjectTaskAssignment = projectTaskAssignment,
                   ProjectTask = projectTask,
                   User = user
               };
    }

    protected virtual IQueryable<ProjectTaskAssignmentWithNavigationProperties> ApplyFilter(IQueryable<ProjectTaskAssignmentWithNavigationProperties> query, string? filterText, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null, Guid? projectTaskId = null, Guid? userId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ProjectTaskAssignment.AssignmentRole!.Contains(filterText!) || e.ProjectTaskAssignment.Note!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(assignmentRole), e => e.ProjectTaskAssignment.AssignmentRole.Contains(assignmentRole)).WhereIf(assignedAtMin.HasValue, e => e.ProjectTaskAssignment.AssignedAt >= assignedAtMin!.Value).WhereIf(assignedAtMax.HasValue, e => e.ProjectTaskAssignment.AssignedAt <= assignedAtMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.ProjectTaskAssignment.Note.Contains(note)).WhereIf(projectTaskId != null && projectTaskId != Guid.Empty, e => e.ProjectTask != null && e.ProjectTask.Id == projectTaskId).WhereIf(userId != null && userId != Guid.Empty, e => e.User != null && e.User.Id == userId);
    }

    public virtual async Task<List<ProjectTaskAssignment>> GetListAsync(string? filterText = null, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, assignmentRole, assignedAtMin, assignedAtMax, note);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskAssignmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null, Guid? projectTaskId = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, assignmentRole, assignedAtMin, assignedAtMax, note, projectTaskId, userId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<ProjectTaskAssignment> ApplyFilter(IQueryable<ProjectTaskAssignment> query, string? filterText = null, string? assignmentRole = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, string? note = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.AssignmentRole!.Contains(filterText!) || e.Note!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(assignmentRole), e => e.AssignmentRole.Contains(assignmentRole)).WhereIf(assignedAtMin.HasValue, e => e.AssignedAt >= assignedAtMin!.Value).WhereIf(assignedAtMax.HasValue, e => e.AssignedAt <= assignedAtMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.Note.Contains(note));
    }

    public virtual async Task<List<ProjectTaskAssignmentWithNavigationProperties>> GetListWithNavigationPropertiesByProjectTaskIdsAsync(
        List<Guid> projectTaskIds,
        CancellationToken cancellationToken = default)
    {
        // Load all assignments for a list of tasks in one query to avoid N+1 calls.
        if (projectTaskIds == null || projectTaskIds.Count == 0)
        {
            return new List<ProjectTaskAssignmentWithNavigationProperties>();
        }

        var query = await GetQueryForNavigationPropertiesAsync();
        query = query.Where(x => projectTaskIds.Contains(x.ProjectTaskAssignment.ProjectTaskId));
        return await query.ToListAsync(GetCancellationToken(cancellationToken));
    }
}