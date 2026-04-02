using HC.Projects;
using HC.ProjectTaskDocuments;
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
using HC.ProjectTaskAssignments;
using Microsoft.Extensions.Logging;

namespace HC.ProjectTasks;

public abstract class EfCoreProjectTaskRepositoryBase : EfCoreRepository<HCDbContext, ProjectTask, Guid>
{
    private readonly ILogger<EfCoreProjectTaskRepositoryBase> _logger;
    public EfCoreProjectTaskRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider, ILogger<EfCoreProjectTaskRepositoryBase> logger) : base(dbContextProvider)
    {
        _logger = logger;
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, CancellationToken cancellationToken = default)
    {
        // var query = await GetQueryForNavigationPropertiesAsync();
        // query = ApplyFilter(query, filterText, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId);
        var query = await GetQueryForNavigationPropertiesAsync(
            filterText, false, false, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId, null
        );
        var ids = query.Select(x => x.ProjectTask.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<ProjectTaskWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        var projectTask = await (await GetDbSetAsync()).FirstOrDefaultAsync(b => b.Id == id && !b.IsDeleted, cancellationToken);
        if (projectTask == null)
        {
            return null;
        }
        
        // Allow Project to be null if it's been deleted (soft delete)
        var project = await dbContext.Set<Project>().FirstOrDefaultAsync(c => c.Id == projectTask.ProjectId && !c.IsDeleted, cancellationToken);
        
        // Get child task count
        var childTaskCount = await (await GetDbSetAsync())
            .CountAsync(pt => !string.IsNullOrWhiteSpace(pt.ParentTaskId) && pt.ParentTaskId == projectTask.Code && !pt.IsDeleted, cancellationToken);
        
        return new ProjectTaskWithNavigationProperties 
        { 
            ProjectTask = projectTask, 
            Project = project,
            // Child collections are enriched later in application service (best-effort).
            ProjectTaskAssignments = new List<ProjectTaskAssignment>(),
            ProjectTaskDocumentsCount = 0,
            ChildTaskCount = childTaskCount
        };
    }

    public virtual async Task<List<ProjectTaskWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool onlyParentTasks = false, bool onlyChildTasks = false, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, 
    Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(
            filterText, onlyParentTasks, onlyChildTasks, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId, userId
        );
        // query = ApplyFilter(query, filterText, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<ProjectTaskWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return await GetQueryForNavigationPropertiesAsync(
            filterText: null,
            onlyParentTasks: false,
            onlyChildTasks: false,
            parentTaskId: null,
            code: null,
            title: null,
            description: null,
            startDateMin: null,
            startDateMax: null,
            dueDateMin: null,
            dueDateMax: null,
            priority: null,
            status: null,
            progressPercentMin: null,
            progressPercentMax: null,
            projectId: null,
            userId: null);
    }

    // protected virtual IQueryable<ProjectTaskWithNavigationProperties> ApplyFilter(IQueryable<ProjectTaskWithNavigationProperties> query, string? filterText, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, Guid? userId = null)
    // {
    //     return query.Where(e => !e.ProjectTask.IsDeleted && (e.Project == null || !e.Project.IsDeleted))
    //         .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ProjectTask.ParentTaskId!.Contains(filterText!) || e.ProjectTask.Code!.Contains(filterText!) || e.ProjectTask.Title!.Contains(filterText!) || e.ProjectTask.Description!.Contains(filterText!) || e.ProjectTask.Priority!.Contains(filterText!) || e.ProjectTask.Status!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(parentTaskId), e => e.ProjectTask.ParentTaskId.Contains(parentTaskId)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.ProjectTask.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(title), e => e.ProjectTask.Title.Contains(title)).WhereIf(!string.IsNullOrWhiteSpace(description), e => e.ProjectTask.Description.Contains(description)).WhereIf(startDateMin.HasValue, e => e.ProjectTask.StartDate >= startDateMin!.Value).WhereIf(startDateMax.HasValue, e => e.ProjectTask.StartDate <= startDateMax!.Value)
    //         .WhereIf(dueDateMin.HasValue, e => e.ProjectTask.DueDate >= dueDateMin!.Value)
    //         .WhereIf(dueDateMax.HasValue, e => e.ProjectTask.DueDate <= dueDateMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(priority), e => e.ProjectTask.Priority.Contains(priority)).WhereIf(!string.IsNullOrWhiteSpace(status), e => e.ProjectTask.Status.Contains(status)).WhereIf(progressPercentMin.HasValue, e => e.ProjectTask.ProgressPercent >= progressPercentMin!.Value).WhereIf(progressPercentMax.HasValue, e => e.ProjectTask.ProgressPercent <= progressPercentMax!.Value).WhereIf(projectId != null && projectId != Guid.Empty, e => e.Project != null && e.Project.Id == projectId)
    //         .WhereIf(userId != null && userId != Guid.Empty, e => (e.ProjectTaskAssignments != null && e.ProjectTaskAssignments.Any(pta => pta.UserId == userId))
    //         || (e.ProjectTask.CreatorId == userId));
    // }

    public virtual async Task<List<ProjectTask>> GetListAsync(string? filterText = null, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? ProjectTaskConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }
    
    protected virtual IQueryable<ProjectTask> ApplyFilter(IQueryable<ProjectTask> query, string? filterText = null, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null)
    {
        return query.Where(e => !e.IsDeleted)
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ParentTaskId!.Contains(filterText!) || e.Code!.Contains(filterText!) || e.Title!.Contains(filterText!) || e.Description!.Contains(filterText!) || e.Priority!.Contains(filterText!) || e.Status!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(parentTaskId), e => e.ParentTaskId.Contains(parentTaskId)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(title), e => e.Title.Contains(title)).WhereIf(!string.IsNullOrWhiteSpace(description), e => e.Description.Contains(description)).WhereIf(startDateMin.HasValue, e => e.StartDate >= startDateMin!.Value).WhereIf(startDateMax.HasValue, e => e.StartDate <= startDateMax!.Value).WhereIf(dueDateMin.HasValue, e => e.DueDate >= dueDateMin!.Value).WhereIf(dueDateMax.HasValue, e => e.DueDate <= dueDateMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(priority), e => e.Priority.Contains(priority)).WhereIf(!string.IsNullOrWhiteSpace(status), e => e.Status.Contains(status)).WhereIf(progressPercentMin.HasValue, e => e.ProgressPercent >= progressPercentMin!.Value).WhereIf(progressPercentMax.HasValue, e => e.ProgressPercent <= progressPercentMax!.Value);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, bool onlyParentTasks = false, bool onlyChildTasks = false, string? parentTaskId = null, 
    string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null,
     DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, 
     string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, 
     Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync(
            filterText, onlyParentTasks, onlyChildTasks, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId, userId
        );
        // query = ApplyFilter(query, filterText, parentTaskId, code, title, description, startDateMin, startDateMax, dueDateMin, dueDateMax, priority, status, progressPercentMin, progressPercentMax, projectId, userId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }





    
   protected virtual async Task<IQueryable<ProjectTaskWithNavigationProperties>> GetQueryForNavigationPropertiesAsync(
       string? filterText, 
       bool onlyParentTasks,
       bool onlyChildTasks,
       string? parentTaskId, string? code,
        string? title, string? description,
         DateTime? startDateMin, DateTime? 
         startDateMax, DateTime? dueDateMin, 
         DateTime? dueDateMax, string? priority,
          string? status, int? progressPercentMin, 
          int? progressPercentMax, Guid? projectId,   
        Guid? userId)
    {
        var dbContext = await GetDbContextAsync();
        var projectTasks = (await GetDbSetAsync()).AsNoTracking();
        projectTasks = projectTasks
            .WhereIf(onlyParentTasks, pt => string.IsNullOrWhiteSpace(pt.ParentTaskId))
            .WhereIf(onlyChildTasks, pt => !string.IsNullOrWhiteSpace(pt.ParentTaskId))
            .WhereIf(!string.IsNullOrWhiteSpace(filterText),
                pt => pt.Code!.Contains(filterText!)
                || pt.Title!.Contains(filterText!)
                || pt.Description!.Contains(filterText!)
                || pt.Status!.Contains(filterText!))
            .WhereIf(!string.IsNullOrWhiteSpace(code), pt => pt.Code.Contains(code))
            .WhereIf(!string.IsNullOrWhiteSpace(title), pt => pt.Title.Contains(title))
            .WhereIf(!string.IsNullOrWhiteSpace(description), pt => pt.Description.Contains(description))
            .WhereIf(startDateMin.HasValue, pt => pt.StartDate >= startDateMin)
            .WhereIf(startDateMax.HasValue, pt => pt.StartDate <= startDateMax)
            .WhereIf(dueDateMin.HasValue, pt => pt.DueDate >= dueDateMin)
            .WhereIf(dueDateMax.HasValue, pt => pt.DueDate <= dueDateMax)
            .WhereIf(!string.IsNullOrWhiteSpace(status), pt => pt.Status.Contains(status))
            .WhereIf(!string.IsNullOrWhiteSpace(parentTaskId), pt => pt.ParentTaskId.Contains(parentTaskId))
            .WhereIf(!string.IsNullOrWhiteSpace(priority), pt => pt.Priority.Contains(priority))
            .WhereIf(progressPercentMin.HasValue, pt => pt.ProgressPercent >= progressPercentMin)
            .WhereIf(progressPercentMax.HasValue, pt => pt.ProgressPercent <= progressPercentMax)
            .WhereIf(projectId.HasValue && projectId != Guid.Empty, pt => pt.ProjectId == projectId)
            .WhereIf(userId.HasValue && userId != Guid.Empty, pt => pt.CreatorId == userId || dbContext.Set<ProjectTaskAssignment>().Any(pta => pta.ProjectTaskId == pt.Id && pta.UserId == userId));

        return
            from projectTask in projectTasks.Where(pt => !pt.IsDeleted)
            join project in dbContext.Set<Project>().Where(p => !p.IsDeleted) on projectTask.ProjectId equals project.Id into projects
            from project in projects.DefaultIfEmpty()
            select new ProjectTaskWithNavigationProperties
            {
                ProjectTask = projectTask,
                Project = project,
                ProjectTaskAssignments = new List<ProjectTaskAssignment>(),
                ProjectTaskDocumentsCount = 0,
                ChildTaskCount = projectTasks.Count(pt => !string.IsNullOrWhiteSpace(pt.ParentTaskId) && pt.ParentTaskId == projectTask.Code && !pt.IsDeleted)
            };
    }
}