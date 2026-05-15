using HC.Shared;
using Volo.Abp.Identity;
using HC.ProjectTasks;
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
using HC.ProjectTaskAssignments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.ProjectTaskAssignments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.ProjectTaskAssignments.Default)]
public abstract class ProjectTaskAssignmentsAppServiceBase : HCAppService
{
    protected IDistributedCache<ProjectTaskAssignmentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IProjectTaskAssignmentRepository _projectTaskAssignmentRepository;
    protected ProjectTaskAssignmentManager _projectTaskAssignmentManager;
    protected IRepository<HC.ProjectTasks.ProjectTask, Guid> _projectTaskRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public ProjectTaskAssignmentsAppServiceBase(IProjectTaskAssignmentRepository projectTaskAssignmentRepository, ProjectTaskAssignmentManager projectTaskAssignmentManager, IDistributedCache<ProjectTaskAssignmentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.ProjectTasks.ProjectTask, Guid> projectTaskRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _projectTaskAssignmentRepository = projectTaskAssignmentRepository;
        _projectTaskAssignmentManager = projectTaskAssignmentManager;
        _projectTaskRepository = projectTaskRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<ProjectTaskAssignmentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskAssignmentsInput input)
    {
        var totalCount = await _projectTaskAssignmentRepository.GetCountAsync(input.FilterText, input.AssignmentRole, input.AssignedAtMin, input.AssignedAtMax, input.Note, input.ProjectTaskId, input.UserId);
        var items = await _projectTaskAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AssignmentRole, input.AssignedAtMin, input.AssignedAtMax, input.Note, input.ProjectTaskId, input.UserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ProjectTaskAssignmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ProjectTaskAssignmentWithNavigationProperties>, List<ProjectTaskAssignmentWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<ProjectTaskAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTaskAssignmentWithNavigationProperties, ProjectTaskAssignmentWithNavigationPropertiesDto>(await _projectTaskAssignmentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<ProjectTaskAssignmentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTaskAssignment, ProjectTaskAssignmentDto>(await _projectTaskAssignmentRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input)
    {
        var query = (await _projectTaskRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Title != null && x.Title.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.ProjectTasks.ProjectTask>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.ProjectTasks.ProjectTask>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.UserName != null && x.UserName.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.ProjectTaskAssignments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _projectTaskAssignmentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.ProjectTaskAssignments.Create)]
    public virtual async Task<ProjectTaskAssignmentDto> CreateAsync(ProjectTaskAssignmentCreateDto input)
    {
        if (input.ProjectTaskId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ProjectTask"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var projectTaskAssignment = await _projectTaskAssignmentManager.CreateAsync(input.ProjectTaskId, input.UserId, input.AssignmentRole, input.AssignedAt, input.Note);
        return ObjectMapper.Map<ProjectTaskAssignment, ProjectTaskAssignmentDto>(projectTaskAssignment);
    }

    [Authorize(HCPermissions.ProjectTaskAssignments.Edit)]
    public virtual async Task<ProjectTaskAssignmentDto> UpdateAsync(Guid id, ProjectTaskAssignmentUpdateDto input)
    {
        if (input.ProjectTaskId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["ProjectTask"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var projectTaskAssignment = await _projectTaskAssignmentManager.UpdateAsync(id, input.ProjectTaskId, input.UserId, input.AssignmentRole, input.AssignedAt, input.Note, input.ConcurrencyStamp);
        return ObjectMapper.Map<ProjectTaskAssignment, ProjectTaskAssignmentDto>(projectTaskAssignment);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskAssignmentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var projectTaskAssignments = await _projectTaskAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.AssignmentRole, input.AssignedAtMin, input.AssignedAtMax, input.Note, input.ProjectTaskId, input.UserId);
        var items = projectTaskAssignments.Select(item => new { AssignmentRole = item.ProjectTaskAssignment.AssignmentRole, AssignedAt = item.ProjectTaskAssignment.AssignedAt, Note = item.ProjectTaskAssignment.Note, ProjectTask = item.ProjectTask?.Title, User = item.User?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "ProjectTaskAssignments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.ProjectTaskAssignments.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> projecttaskassignmentIds)
    {
        await _projectTaskAssignmentRepository.DeleteManyAsync(projecttaskassignmentIds);
    }

    [Authorize(HCPermissions.ProjectTaskAssignments.Delete)]
    public virtual async Task DeleteAllAsync(GetProjectTaskAssignmentsInput input)
    {
        await _projectTaskAssignmentRepository.DeleteAllAsync(input.FilterText, input.AssignmentRole, input.AssignedAtMin, input.AssignedAtMax, input.Note, input.ProjectTaskId, input.UserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ProjectTaskAssignmentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}