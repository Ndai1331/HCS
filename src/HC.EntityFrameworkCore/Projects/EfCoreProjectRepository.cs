using HC.Departments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using HC.ProjectMembers;
using HC.ProjectTasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;

namespace HC.Projects;

public abstract class EfCoreProjectRepositoryBase : EfCoreRepository<HCDbContext, Project, Guid>
{
    public EfCoreProjectRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null, Guid? ownerDepartmentId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, description, startDateMin, startDateMax, endDateMin, endDateMax, status, ownerDepartmentId, null);
        var ids = query.Select(x => x.Project.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ProjectWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(project => new ProjectWithNavigationProperties
        {
            Project = project,
            OwnerDepartment = dbContext.Set<Department>().FirstOrDefault(c => c.Id == project.OwnerDepartmentId),
            ProjectMemberCount = dbContext.Set<ProjectMember>().Count(pm => pm.ProjectId == project.Id),
            ProjectTaskCount = dbContext.Set<ProjectTask>().Count(pt => pt.ProjectId == project.Id)
        }).FirstOrDefault();
    }

    public virtual async Task<List<ProjectWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null, Guid? ownerDepartmentId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(
            filterText, code, name, description, startDateMin, startDateMax, endDateMin, endDateMax, status, ownerDepartmentId, userId
        );
        // query = ApplyFilter(query, filterText, code, name, description, startDateMin, startDateMax, endDateMin, endDateMax, status, ownerDepartmentId, userId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ProjectWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        var dbContext = await GetDbContextAsync();
        var projects = await GetDbSetAsync();

        return
            from project in projects

            join ownerDepartment in dbContext.Set<Department>()
                on project.OwnerDepartmentId equals ownerDepartment.Id into departments
            from ownerDepartment in departments.DefaultIfEmpty()

            join projectMembers in dbContext.Set<ProjectMember>()
                on project.Id equals projectMembers.ProjectId into memberGroup

            join projectTasks in dbContext.Set<ProjectTask>()
                on project.Id equals projectTasks.ProjectId into taskGroup

            select new ProjectWithNavigationProperties
            {
                Project = project,
                OwnerDepartment = ownerDepartment,
                ProjectMembers = memberGroup.ToList(),
                ProjectMemberCount = memberGroup.Count(),
                ProjectTaskCount = taskGroup.Count()
            };
        // return from project in (await GetDbSetAsync())
        //        join ownerDepartment in dbContext.Set<Department>() on project.OwnerDepartmentId equals ownerDepartment.Id into departments
        //        from ownerDepartment in departments.DefaultIfEmpty()

        //        join projectMembers in dbContext.Set<ProjectMember>() on project.Id equals projectMembers.ProjectId into projectMemberss
        //        from projectMembers in projectMemberss.DefaultIfEmpty()
        //        select new ProjectWithNavigationProperties
        //        {
        //            Project = project,
        //            OwnerDepartment = ownerDepartment,
                   
        //            ProjectMemberCount = dbContext.Set<ProjectMember>().Count(pm => pm.ProjectId == project.Id),
        //            ProjectTaskCount = dbContext.Set<ProjectTask>().Count(pt => pt.ProjectId == project.Id)
        //        };
    }


    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null, Guid? ownerDepartmentId = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(
            filterText, code, name, description, startDateMin, startDateMax, endDateMin, endDateMax, status, ownerDepartmentId, userId
        );
        // query = ApplyFilter(query, filterText, code, name, description, startDateMin, startDateMax, endDateMin, endDateMax, status, ownerDepartmentId, userId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<ProjectWithNavigationProperties> ApplyFilter(IQueryable<ProjectWithNavigationProperties> query, string? filterText, string? code = null, string? name = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null, Guid? ownerDepartmentId = null, Guid? userId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Project.Code!.Contains(filterText!) 
        || e.Project.Name!.Contains(filterText!) || e.Project.Description!.Contains(filterText!) 
        || e.Project.Status!.Contains(filterText!))
        
        .WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Project.Code.Contains(code))
        .WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Project.Name.Contains(name))
        .WhereIf(!string.IsNullOrWhiteSpace(description), e => e.Project.Description.Contains(description))
        .WhereIf(startDateMin.HasValue, e => e.Project.StartDate >= startDateMin!.Value)
        .WhereIf(startDateMax.HasValue, e => e.Project.StartDate <= startDateMax!.Value)
        .WhereIf(endDateMin.HasValue, e => e.Project.EndDate >= endDateMin!.Value)
        .WhereIf(endDateMax.HasValue, e => e.Project.EndDate <= endDateMax!.Value)
        .WhereIf(!string.IsNullOrWhiteSpace(status), e => e.Project.Status.Contains(status))
        .WhereIf(ownerDepartmentId != null && ownerDepartmentId != Guid.Empty, e => e.OwnerDepartment != null && e.OwnerDepartment.Id == ownerDepartmentId)
        .WhereIf(userId != null && userId != Guid.Empty, e => (e.ProjectMembers != null && e.ProjectMembers.Any(pm => pm.UserId == userId))
            || (e.Project.CreatorId == userId)
        );
    }



   protected virtual async Task<IQueryable<ProjectWithNavigationProperties>> GetQueryForNavigationPropertiesAsync(
        string? filterText,
        string? code,
        string? name,
        string? description,
        DateTime? startDateMin,
        DateTime? startDateMax,
        DateTime? endDateMin,
        DateTime? endDateMax,
        string? status,
        Guid? ownerDepartmentId,
        Guid? userId)
    {
        var dbContext = await GetDbContextAsync();
        var projects = (await GetDbSetAsync()).AsNoTracking();

        projects = projects
            .WhereIf(!string.IsNullOrWhiteSpace(filterText),
                p => p.Code!.Contains(filterText!)
                || p.Name!.Contains(filterText!)
                || p.Description!.Contains(filterText!)
                || p.Status!.Contains(filterText!))
            .WhereIf(!string.IsNullOrWhiteSpace(code), p => p.Code.Contains(code))
            .WhereIf(!string.IsNullOrWhiteSpace(name), p => p.Name.Contains(name))
            .WhereIf(!string.IsNullOrWhiteSpace(description), p => p.Description.Contains(description))
            .WhereIf(startDateMin.HasValue, p => p.StartDate >= startDateMin)
            .WhereIf(startDateMax.HasValue, p => p.StartDate <= startDateMax)
            .WhereIf(endDateMin.HasValue, p => p.EndDate >= endDateMin)
            .WhereIf(endDateMax.HasValue, p => p.EndDate <= endDateMax)
            .WhereIf(!string.IsNullOrWhiteSpace(status), p => p.Status.Contains(status))
            .WhereIf(ownerDepartmentId.HasValue && ownerDepartmentId != Guid.Empty,
                p => p.OwnerDepartmentId == ownerDepartmentId)

            .WhereIf(userId.HasValue && userId != Guid.Empty,
                p => p.CreatorId == userId
                || dbContext.Set<ProjectMember>()
                        .Any(pm => pm.ProjectId == p.Id && pm.UserId == userId));

        return
            from project in projects
            join dept in dbContext.Set<Department>()
                on project.OwnerDepartmentId equals dept.Id into depts
            from dept in depts.DefaultIfEmpty()

            select new ProjectWithNavigationProperties
            {
                Project = project,
                OwnerDepartment = dept,

                ProjectMemberCount = dbContext.Set<ProjectMember>()
                    .Count(pm => pm.ProjectId == project.Id),

                ProjectTaskCount = dbContext.Set<ProjectTask>()
                    .Count(pt => pt.ProjectId == project.Id)

            };
    }
}