using Volo.Abp.Identity;
using HC.Departments;
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

namespace HC.UserDepartments;

public abstract class EfCoreUserDepartmentRepositoryBase : EfCoreRepository<HCDbContext, UserDepartment, Guid>
{
    public EfCoreUserDepartmentRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? departmentId = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, departmentId, userId);
        var ids = query.Select(x => x.UserDepartment.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<UserDepartmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(userDepartment => new UserDepartmentWithNavigationProperties { UserDepartment = userDepartment, Department = dbContext.Set<Department>().FirstOrDefault(c => c.Id == userDepartment.DepartmentId), User = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == userDepartment.UserId) }).FirstOrDefault();
    }

    public virtual async Task<List<UserDepartmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? departmentId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, departmentId, userId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? UserDepartmentConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<UserDepartmentWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from userDepartment in (await GetDbSetAsync())
               join department in (await GetDbContextAsync()).Set<Department>() on userDepartment.DepartmentId equals department.Id into departments
               from department in departments.DefaultIfEmpty()
               join user in (await GetDbContextAsync()).Set<IdentityUser>() on userDepartment.UserId equals user.Id into identityUsers
               from user in identityUsers.DefaultIfEmpty()
               select new UserDepartmentWithNavigationProperties
               {
                   UserDepartment = userDepartment,
                   Department = department,
                   User = user
               };
    }

    protected virtual IQueryable<UserDepartmentWithNavigationProperties> ApplyFilter(IQueryable<UserDepartmentWithNavigationProperties> query, string? filterText, bool? isPrimary = null, bool? isActive = null, Guid? departmentId = null, Guid? userId = null)
    {
        // Filter by FK on UserDepartment so results are correct even if Department/User join is null or mismatched.
        return query
            .WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)
            .WhereIf(isPrimary.HasValue, e => e.UserDepartment.IsPrimary == isPrimary)
            .WhereIf(isActive.HasValue, e => e.UserDepartment.IsActive == isActive)
            .WhereIf(departmentId.HasValue && departmentId.Value != Guid.Empty, e => e.UserDepartment.DepartmentId == departmentId!.Value)
            .WhereIf(userId.HasValue && userId.Value != Guid.Empty, e => e.UserDepartment.UserId == userId!.Value);
    }

    public virtual async Task<List<UserDepartment>> GetListAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, isPrimary, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? UserDepartmentConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, bool? isPrimary = null, bool? isActive = null, Guid? departmentId = null, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, isPrimary, isActive, departmentId, userId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<UserDepartment> ApplyFilter(IQueryable<UserDepartment> query, string? filterText = null, bool? isPrimary = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(isPrimary.HasValue, e => e.IsPrimary == isPrimary).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}