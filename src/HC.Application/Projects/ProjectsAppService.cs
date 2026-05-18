using HC.Shared;
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
using HC.Projects;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Chat.Helpers;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;

namespace HC.Projects;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.Projects.Default)]
public abstract class ProjectsAppServiceBase : HCAppService
{
    protected IDistributedCache<ProjectDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IProjectRepository _projectRepository;
    protected ProjectManager _projectManager;
    protected IRepository<HC.Departments.Department, Guid> _departmentRepository;
    protected ILogger<ProjectsAppServiceBase> _logger;

    public ProjectsAppServiceBase(IProjectRepository projectRepository, ProjectManager projectManager, IDistributedCache<ProjectDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Departments.Department, Guid> departmentRepository, ILogger<ProjectsAppServiceBase> logger)
    {
        _downloadTokenCache = downloadTokenCache;
        _projectRepository = projectRepository;
        _projectManager = projectManager;
        _departmentRepository = departmentRepository;
        _logger = logger;
    }

    public virtual async Task<PagedResultDto<ProjectWithNavigationPropertiesDto>> GetListAsync(GetProjectsInput input)
    {
        var statusFilter = input.Status?.ToString();
        if (!CurrentUser.IsAdminRole())
        {
            input.UserId = CurrentUser.Id;
        }

        var totalCount = await _projectRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.Description, 
        input.StartDateMin, input.StartDateMax, input.EndDateMin, input.EndDateMax, statusFilter, input.OwnerDepartmentId, input.UserId);

        var items = await _projectRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code,
        input.Name, input.Description, input.StartDateMin, input.StartDateMax, input.EndDateMin, 
        input.EndDateMax, statusFilter, input.OwnerDepartmentId, input.UserId, input.Sorting, input.MaxResultCount, input.SkipCount);

        return new PagedResultDto<ProjectWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ProjectWithNavigationProperties>, List<ProjectWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<ProjectWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectWithNavigationProperties, ProjectWithNavigationPropertiesDto>(await _projectRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<ProjectDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Project, ProjectDto>(await _projectRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input)
    {
        var query = (await _departmentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Departments.Department>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Departments.Department>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.Projects.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var project = await _projectRepository.GetAsync(id);
        if (!CurrentUser.IsAdminRole() && (!CurrentUser.Id.HasValue || project.CreatorId != CurrentUser.Id.Value))
        {
            throw new UserFriendlyException("Only project creator can delete this project.");
        }

        try
        {
            _logger.LogInformation($"Deleting project {id}");
            await _projectRepository.DeleteAsync(id);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error deleting project {id}: {ex.Message}");
            throw new UserFriendlyException(ex.Message);
        }
    }

    [Authorize(HCPermissions.Projects.Create)]
    public virtual async Task<ProjectDto> CreateAsync(ProjectCreateDto input)
    {
        // Convert enum to string for repository
        var statusString = input.Status.ToString();
        var project = await _projectManager.CreateAsync(input.OwnerDepartmentId, input.Code, input.Name, input.StartDate, input.EndDate, statusString, input.Description);
        return ObjectMapper.Map<Project, ProjectDto>(project);
    }

    [Authorize(HCPermissions.Projects.Edit)]
    public virtual async Task<ProjectDto> UpdateAsync(Guid id, ProjectUpdateDto input)
    {
        var project = await _projectRepository.GetAsync(id);
        if (!CurrentUser.IsAdminRole() && (!CurrentUser.Id.HasValue || project.CreatorId != CurrentUser.Id.Value))
        {
            throw new UserFriendlyException("Only project creator can update this project.");
        }

        // Convert enum to string for repository
        var statusString = input.Status.ToString();
        var updatedProject = await _projectManager.UpdateAsync(id, input.OwnerDepartmentId, input.Code, input.Name, input.StartDate, input.EndDate, statusString, input.Description, input.ConcurrencyStamp);
        return ObjectMapper.Map<Project, ProjectDto>(updatedProject);
    }

    /// <summary>Excel download: anonymous endpoint allowed only with a one-time server-issued token (see <see cref="GetDownloadTokenAsync"/>).</summary>
    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var projects = await _projectRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.Description, input.StartDateMin, input.StartDateMax, input.EndDateMin, input.EndDateMax, input.Status, input.OwnerDepartmentId);
        var items = projects.Select(item => new { Code = item.Project.Code, Name = item.Project.Name, Description = item.Project.Description, StartDate = item.Project.StartDate, EndDate = item.Project.EndDate, Status = item.Project.Status, OwnerDepartment = item.OwnerDepartment?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Projects.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.Projects.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> projectIds)
    {
        await _projectRepository.DeleteManyAsync(projectIds);
    }

    [Authorize(HCPermissions.Projects.Delete)]
    public virtual async Task DeleteAllAsync(GetProjectsInput input)
    {
        // Convert enum to string for repository
        var statusFilter = input.Status?.ToString();
        await _projectRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.Description, input.StartDateMin, input.StartDateMax, input.EndDateMin, input.EndDateMax, statusFilter, input.OwnerDepartmentId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ProjectDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}
