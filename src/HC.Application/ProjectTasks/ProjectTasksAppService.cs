using HC.Shared;
using HC.Projects;
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
using HC.ProjectTasks;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Chat.Helpers;
using HC.ProjectMembers;

namespace HC.ProjectTasks;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.ProjectTasks.Default)]
public abstract class ProjectTasksAppServiceBase : HCAppService
{
    protected IDistributedCache<ProjectTaskDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IProjectTaskRepository _projectTaskRepository;
    protected ProjectTaskManager _projectTaskManager;
    protected IRepository<HC.Projects.Project, Guid> _projectRepository;
    protected IProjectMemberRepository _projectMemberRepository;

    public ProjectTasksAppServiceBase(IProjectTaskRepository projectTaskRepository, ProjectTaskManager projectTaskManager, IDistributedCache<ProjectTaskDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Projects.Project, Guid> projectRepository, IProjectMemberRepository projectMemberRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _projectTaskRepository = projectTaskRepository;
        _projectTaskManager = projectTaskManager;
        _projectRepository = projectRepository;
        _projectMemberRepository = projectMemberRepository;
    }

    public virtual async Task<PagedResultDto<ProjectTaskWithNavigationPropertiesDto>> GetListAsync(GetProjectTasksInput input)
    {
        if (!CurrentUser.IsAdminRole())
        {
            input.UserId = CurrentUser.Id;
        }
        var totalCount = await _projectTaskRepository.GetCountAsync(input.FilterText, input.OnlyParentTasks, input.OnlyChildTasks, input.ParentTaskId, input.Code, input.Title, input.Description, input.StartDateMin, input.StartDateMax, input.DueDateMin, input.DueDateMax, input.Priority, input.Status, input.ProgressPercentMin, input.ProgressPercentMax, input.ProjectId, input.UserId   );
        var items = await _projectTaskRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.OnlyParentTasks, input.OnlyChildTasks, input.ParentTaskId, input.Code, input.Title, input.Description, input.StartDateMin, input.StartDateMax, input.DueDateMin, input.DueDateMax, input.Priority, input.Status, input.ProgressPercentMin, input.ProgressPercentMax, input.ProjectId, input.UserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ProjectTaskWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ProjectTaskWithNavigationProperties>, List<ProjectTaskWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<ProjectTaskWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTaskWithNavigationProperties, ProjectTaskWithNavigationPropertiesDto>(await _projectTaskRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<ProjectTaskDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(await _projectTaskRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input)
    {
        var query = await _projectRepository.GetQueryableAsync();

        if (!CurrentUser.IsAdminRole())
        {
            var userId = CurrentUser.Id;
            if (userId.HasValue)
            {
                var projectMemberQuery = await _projectMemberRepository.GetQueryableAsync();
                query =
                    from project in query
                    where project.CreatorId == userId.Value
                        || projectMemberQuery.Any(member => member.ProjectId == project.Id && member.UserId == userId.Value)
                    select project;
            }
            else
            {
                query = query.Where(_ => false);
            }
        }

        query = query.WhereIf(
            !string.IsNullOrWhiteSpace(input.Filter),
            x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter))
        );

        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Projects.Project>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Projects.Project>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.ProjectTasks.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var projectTask = await _projectTaskRepository.GetAsync(id);
        
        // Check if current user is the creator or admin
        if (!CurrentUser.Id.Equals(projectTask.CreatorId) && !CurrentUser.IsAdminRole())
        {
            throw new UserFriendlyException(L["OnlyTaskCreatorCanDelete"]);
        }
        
        await _projectTaskRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.ProjectTasks.Create)]
    public virtual async Task<ProjectTaskDto> CreateAsync(ProjectTaskCreateDto input)
    {
        if (input.ProjectId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Project"]]);
        }

        var projectTask = await _projectTaskManager.CreateAsync(input.ProjectId, input.Code, input.Title, input.StartDate, input.DueDate, input.Priority, input.Status, input.ProgressPercent, input.ParentTaskId, input.Description);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(projectTask);
    }

    [Authorize(HCPermissions.ProjectTasks.Edit)]
    public virtual async Task<ProjectTaskDto> UpdateAsync(Guid id, ProjectTaskUpdateDto input)
    {
        if (input.ProjectId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Project"]]);
        }

        var projectTask = await _projectTaskManager.UpdateAsync(id, input.ProjectId, input.Code, input.Title, input.StartDate, input.DueDate, input.Priority, input.Status, input.ProgressPercent, input.ParentTaskId, input.Description, input.ConcurrencyStamp);
        return ObjectMapper.Map<ProjectTask, ProjectTaskDto>(projectTask);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var projectTasks = await _projectTaskRepository.GetListWithNavigationPropertiesAsync(input.FilterText, false, false, input.ParentTaskId, input.Code, input.Title, input.Description, input.StartDateMin, input.StartDateMax, input.DueDateMin, input.DueDateMax, input.Priority, input.Status, input.ProgressPercentMin, input.ProgressPercentMax, input.ProjectId, null, null, int.MaxValue, 0);
        var items = projectTasks.Select(item => new { ParentTaskId = item.ProjectTask.ParentTaskId, Code = item.ProjectTask.Code, Title = item.ProjectTask.Title, Description = item.ProjectTask.Description, StartDate = item.ProjectTask.StartDate, DueDate = item.ProjectTask.DueDate, Priority = item.ProjectTask.Priority, Status = item.ProjectTask.Status, ProgressPercent = item.ProjectTask.ProgressPercent, Project = item.Project?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "ProjectTasks.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.ProjectTasks.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> projecttaskIds)
    {
        await _projectTaskRepository.DeleteManyAsync(projecttaskIds);
    }

    [Authorize(HCPermissions.ProjectTasks.Delete)]
    public virtual async Task DeleteAllAsync(GetProjectTasksInput input)
    {
        await _projectTaskRepository.DeleteAllAsync(input.FilterText, input.ParentTaskId, input.Code, input.Title, input.Description, input.StartDateMin, input.StartDateMax, input.DueDateMin, input.DueDateMax, input.Priority, input.Status, input.ProgressPercentMin, input.ProgressPercentMax, input.ProjectId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ProjectTaskDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}
