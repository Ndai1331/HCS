using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.ProjectTasks;
using Volo.Abp.Content;

namespace HC.Controllers.ProjectTasks;

[RemoteService]
[Area("app")]
[ControllerName("ProjectTask")]
[Route("api/app/project-tasks")]
public abstract class ProjectTaskControllerBase : AbpController
{
    protected IProjectTasksAppService _projectTasksAppService;

    public ProjectTaskControllerBase(IProjectTasksAppService projectTasksAppService)
    {
        _projectTasksAppService = projectTasksAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ProjectTaskWithNavigationPropertiesDto>> GetListAsync(GetProjectTasksInput input)
    {
        return _projectTasksAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ProjectTaskWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _projectTasksAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ProjectTaskDto> GetAsync(Guid id)
    {
        return _projectTasksAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("project-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input)
    {
        return _projectTasksAppService.GetProjectLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ProjectTaskDto> CreateAsync(ProjectTaskCreateDto input)
    {
        return _projectTasksAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ProjectTaskDto> UpdateAsync(Guid id, ProjectTaskUpdateDto input)
    {
        return _projectTasksAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _projectTasksAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskExcelDownloadDto input)
    {
        return _projectTasksAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _projectTasksAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> projecttaskIds)
    {
        return _projectTasksAppService.DeleteByIdsAsync(projecttaskIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetProjectTasksInput input)
    {
        return _projectTasksAppService.DeleteAllAsync(input);
    }
}