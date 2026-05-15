using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.Projects;
using Volo.Abp.Content;

namespace HC.Controllers.Projects;

[RemoteService]
[Area("app")]
[ControllerName("Project")]
[Route("api/app/projects")]
public abstract class ProjectControllerBase : AbpController
{
    protected IProjectsAppService _projectsAppService;

    public ProjectControllerBase(IProjectsAppService projectsAppService)
    {
        _projectsAppService = projectsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ProjectWithNavigationPropertiesDto>> GetListAsync(GetProjectsInput input)
    {
        return _projectsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ProjectWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _projectsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ProjectDto> GetAsync(Guid id)
    {
        return _projectsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("department-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input)
    {
        return _projectsAppService.GetDepartmentLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ProjectDto> CreateAsync(ProjectCreateDto input)
    {
        return _projectsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ProjectDto> UpdateAsync(Guid id, ProjectUpdateDto input)
    {
        return _projectsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _projectsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectExcelDownloadDto input)
    {
        return _projectsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _projectsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> projectIds)
    {
        return _projectsAppService.DeleteByIdsAsync(projectIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetProjectsInput input)
    {
        return _projectsAppService.DeleteAllAsync(input);
    }
}