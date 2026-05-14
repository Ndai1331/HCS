using HC.Shared;
using Volo.Abp.Identity;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.Departments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.Departments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.MasterDatas.DepartmentDefault)]
public abstract class DepartmentsAppServiceBase : HCAppService
{
    protected IDistributedCache<DepartmentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDepartmentRepository _departmentRepository;
    protected DepartmentManager _departmentManager;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public DepartmentsAppServiceBase(IDepartmentRepository departmentRepository, DepartmentManager departmentManager, IDistributedCache<DepartmentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _departmentRepository = departmentRepository;
        _departmentManager = departmentManager;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<DepartmentWithNavigationPropertiesDto>> GetListAsync(GetDepartmentsInput input)
    {
        var totalCount = await _departmentRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.ParentId, input.LevelMin, input.LevelMax, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.LeaderUserId);
        var items = await _departmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.ParentId, input.LevelMin, input.LevelMax, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.LeaderUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DepartmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DepartmentWithNavigationProperties>, List<DepartmentWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DepartmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DepartmentWithNavigationProperties, DepartmentWithNavigationPropertiesDto>(await _departmentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DepartmentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Department, DepartmentDto>(await _departmentRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.UserName != null && x.UserName.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.MasterDatas.DepartmentDelete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _departmentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.MasterDatas.DepartmentCreate)]
    public virtual async Task<DepartmentDto> CreateAsync(DepartmentCreateDto input)
    {
        var department = await _departmentManager.CreateAsync(input.LeaderUserId, input.Code, input.Name, input.Level, input.SortOrder, input.IsActive, input.ParentId);
        return ObjectMapper.Map<Department, DepartmentDto>(department);
    }

    [Authorize(HCPermissions.MasterDatas.DepartmentEdit)]
    public virtual async Task<DepartmentDto> UpdateAsync(Guid id, DepartmentUpdateDto input)
    {
        var department = await _departmentManager.UpdateAsync(id, input.LeaderUserId, input.Code, input.Name, input.Level, input.SortOrder, input.IsActive, input.ParentId, input.ConcurrencyStamp);
        return ObjectMapper.Map<Department, DepartmentDto>(department);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DepartmentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var departments = await _departmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.ParentId, input.LevelMin, input.LevelMax, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.LeaderUserId);
        var items = departments.Select(item => new { Code = item.Department.Code, Name = item.Department.Name, ParentId = item.Department.ParentId, Level = item.Department.Level, SortOrder = item.Department.SortOrder, IsActive = item.Department.IsActive, LeaderUser = item.LeaderUser?.UserName, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Departments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.MasterDatas.DepartmentDelete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> departmentIds)
    {
        await _departmentRepository.DeleteManyAsync(departmentIds);
    }

    [Authorize(HCPermissions.MasterDatas.DepartmentDelete)]
    public virtual async Task DeleteAllAsync(GetDepartmentsInput input)
    {
        await _departmentRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.ParentId, input.LevelMin, input.LevelMax, input.SortOrderMin, input.SortOrderMax, input.IsActive, input.LeaderUserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DepartmentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}