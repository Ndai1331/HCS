using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.ProjectTaskAssignments;
using Volo.Abp.Content;

namespace HC.Controllers.ProjectTaskAssignments;

[RemoteService]
[Area("app")]
[ControllerName("ProjectTaskAssignment")]
[Route("api/app/project-task-assignments")]
public abstract class ProjectTaskAssignmentControllerBase : AbpController
{
    protected IProjectTaskAssignmentsAppService _projectTaskAssignmentsAppService;

    public ProjectTaskAssignmentControllerBase(IProjectTaskAssignmentsAppService projectTaskAssignmentsAppService)
    {
        _projectTaskAssignmentsAppService = projectTaskAssignmentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ProjectTaskAssignmentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskAssignmentsInput input)
    {
        return _projectTaskAssignmentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ProjectTaskAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _projectTaskAssignmentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ProjectTaskAssignmentDto> GetAsync(Guid id)
    {
        return _projectTaskAssignmentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("project-task-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input)
    {
        return _projectTaskAssignmentsAppService.GetProjectTaskLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _projectTaskAssignmentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ProjectTaskAssignmentDto> CreateAsync(ProjectTaskAssignmentCreateDto input)
    {
        return _projectTaskAssignmentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ProjectTaskAssignmentDto> UpdateAsync(Guid id, ProjectTaskAssignmentUpdateDto input)
    {
        return _projectTaskAssignmentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _projectTaskAssignmentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskAssignmentExcelDownloadDto input)
    {
        return _projectTaskAssignmentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _projectTaskAssignmentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> projecttaskassignmentIds)
    {
        return _projectTaskAssignmentsAppService.DeleteByIdsAsync(projecttaskassignmentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetProjectTaskAssignmentsInput input)
    {
        return _projectTaskAssignmentsAppService.DeleteAllAsync(input);
    }
}