using Volo.Abp.Identity;
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

namespace HC.Departments;

public abstract class EfCoreDepartmentRepositoryBase : EfCoreRepository<HCDbContext, Department, Guid>
{
    public EfCoreDepartmentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null, Guid? leaderUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, parentId, levelMin, levelMax, sortOrderMin, sortOrderMax, isActive, leaderUserId);
        var ids = query.Select(x => x.Department.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<DepartmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.Department.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<DepartmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null, Guid? leaderUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, parentId, levelMin, levelMax, sortOrderMin, sortOrderMax, isActive, leaderUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DepartmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<DepartmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        var dbContext = await GetDbContextAsync();
        var departments = await GetDbSetAsync();
        
        return from department in departments
               join leaderUser in dbContext.Set<IdentityUser>() on department.LeaderUserId equals leaderUser.Id into identityUsers
               from leaderUser in identityUsers.DefaultIfEmpty()
               select new DepartmentWithNavigationProperties
               {
                   Department = department,
                   LeaderUser = leaderUser
               };
    }

    protected virtual IQueryable<DepartmentWithNavigationProperties> ApplyFilter(IQueryable<DepartmentWithNavigationProperties> query, string? filterText, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null, Guid? leaderUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Department.Code!.Contains(filterText!) || e.Department.Name!.Contains(filterText!) || e.Department.ParentId!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Department.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Department.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(parentId), e => e.Department.ParentId.Contains(parentId)).WhereIf(levelMin.HasValue, e => e.Department.Level >= levelMin!.Value).WhereIf(levelMax.HasValue, e => e.Department.Level <= levelMax!.Value).WhereIf(sortOrderMin.HasValue, e => e.Department.SortOrder >= sortOrderMin!.Value).WhereIf(sortOrderMax.HasValue, e => e.Department.SortOrder <= sortOrderMax!.Value).WhereIf(isActive.HasValue, e => e.Department.IsActive == isActive).WhereIf(leaderUserId != null && leaderUserId != Guid.Empty, e => e.LeaderUser != null && e.LeaderUser.Id == leaderUserId);
    }

    public virtual async Task<List<Department>> GetListAsync(string? filterText = null, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, code, name, parentId, levelMin, levelMax, sortOrderMin, sortOrderMax, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? DepartmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null, Guid? leaderUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, parentId, levelMin, levelMax, sortOrderMin, sortOrderMax, isActive, leaderUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<Department> ApplyFilter(IQueryable<Department> query, string? filterText = null, string? code = null, string? name = null, string? parentId = null, int? levelMin = null, int? levelMax = null, int? sortOrderMin = null, int? sortOrderMax = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Code!.Contains(filterText!) || e.Name!.Contains(filterText!) || e.ParentId!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(parentId), e => e.ParentId.Contains(parentId)).WhereIf(levelMin.HasValue, e => e.Level >= levelMin!.Value).WhereIf(levelMax.HasValue, e => e.Level <= levelMax!.Value).WhereIf(sortOrderMin.HasValue, e => e.SortOrder >= sortOrderMin!.Value).WhereIf(sortOrderMax.HasValue, e => e.SortOrder <= sortOrderMax!.Value).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}