using HC.Shared;
using Volo.Abp.Identity;
using HC.Departments;
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
using HC.UserDepartments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.UserDepartments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.UserDepartments.Default)]
public abstract class UserDepartmentsAppServiceBase : HCAppService
{
    protected IDistributedCache<UserDepartmentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IUserDepartmentRepository _userDepartmentRepository;
    protected UserDepartmentManager _userDepartmentManager;
    protected IRepository<HC.Departments.Department, Guid> _departmentRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public UserDepartmentsAppServiceBase(IUserDepartmentRepository userDepartmentRepository, UserDepartmentManager userDepartmentManager, IDistributedCache<UserDepartmentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Departments.Department, Guid> departmentRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _userDepartmentRepository = userDepartmentRepository;
        _userDepartmentManager = userDepartmentManager;
        _departmentRepository = departmentRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<UserDepartmentWithNavigationPropertiesDto>> GetListAsync(GetUserDepartmentsInput input)
    {
        // Check if current user is admin
        var isAdmin = CurrentUser.IsInRole("admin");

        // If not admin, filter by current user's UserId
        Guid? filterUserId = input.UserId;
        if (!isAdmin && CurrentUser.Id.HasValue)
        {
            filterUserId = CurrentUser.Id.Value;
        }

        var totalCount = await _userDepartmentRepository.GetCountAsync(input.FilterText, input.IsPrimary, input.IsActive, input.DepartmentId, filterUserId);
        var items = await _userDepartmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsPrimary, input.IsActive, input.DepartmentId, filterUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<UserDepartmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<UserDepartmentWithNavigationProperties>, List<UserDepartmentWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<UserDepartmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<UserDepartmentWithNavigationProperties, UserDepartmentWithNavigationPropertiesDto>(await _userDepartmentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<UserDepartmentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<UserDepartment, UserDepartmentDto>(await _userDepartmentRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input)
    {
        var query = (await _departmentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Departments.Department>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Departments.Department>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.UserDepartments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _userDepartmentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.UserDepartments.Create)]
    public virtual async Task<UserDepartmentDto> CreateAsync(UserDepartmentCreateDto input)
    {
        if (input.DepartmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Department"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var userDepartment = await _userDepartmentManager.CreateAsync(input.DepartmentId, input.UserId, input.IsPrimary, input.IsActive);
        return ObjectMapper.Map<UserDepartment, UserDepartmentDto>(userDepartment);
    }

    [Authorize(HCPermissions.UserDepartments.Edit)]
    public virtual async Task<UserDepartmentDto> UpdateAsync(Guid id, UserDepartmentUpdateDto input)
    {
        if (input.DepartmentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Department"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var userDepartment = await _userDepartmentManager.UpdateAsync(id, input.DepartmentId, input.UserId, input.IsPrimary, input.IsActive, input.ConcurrencyStamp);
        return ObjectMapper.Map<UserDepartment, UserDepartmentDto>(userDepartment);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserDepartmentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var userDepartments = await _userDepartmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsPrimary, input.IsActive, input.DepartmentId, input.UserId);
        var items = userDepartments.Select(item => new { IsPrimary = item.UserDepartment.IsPrimary, IsActive = item.UserDepartment.IsActive, Department = item.Department?.Name, User = item.User?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "UserDepartments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.UserDepartments.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> userdepartmentIds)
    {
        await _userDepartmentRepository.DeleteManyAsync(userdepartmentIds);
    }

    [Authorize(HCPermissions.UserDepartments.Delete)]
    public virtual async Task DeleteAllAsync(GetUserDepartmentsInput input)
    {
        await _userDepartmentRepository.DeleteAllAsync(input.FilterText, input.IsPrimary, input.IsActive, input.DepartmentId, input.UserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new UserDepartmentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}