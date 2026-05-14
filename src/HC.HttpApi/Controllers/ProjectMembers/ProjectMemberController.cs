using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.ProjectMembers;
using Volo.Abp.Content;

namespace HC.Controllers.ProjectMembers;

[RemoteService]
[Area("app")]
[ControllerName("ProjectMember")]
[Route("api/app/project-members")]
public abstract class ProjectMemberControllerBase : AbpController
{
    protected IProjectMembersAppService _projectMembersAppService;

    public ProjectMemberControllerBase(IProjectMembersAppService projectMembersAppService)
    {
        _projectMembersAppService = projectMembersAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ProjectMemberWithNavigationPropertiesDto>> GetListAsync(GetProjectMembersInput input)
    {
        return _projectMembersAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ProjectMemberWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _projectMembersAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ProjectMemberDto> GetAsync(Guid id)
    {
        return _projectMembersAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("project-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input)
    {
        return _projectMembersAppService.GetProjectLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _projectMembersAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ProjectMemberDto> CreateAsync(ProjectMemberCreateDto input)
    {
        return _projectMembersAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ProjectMemberDto> UpdateAsync(Guid id, ProjectMemberUpdateDto input)
    {
        return _projectMembersAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _projectMembersAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectMemberExcelDownloadDto input)
    {
        return _projectMembersAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _projectMembersAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> projectmemberIds)
    {
        return _projectMembersAppService.DeleteByIdsAsync(projectmemberIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetProjectMembersInput input)
    {
        return _projectMembersAppService.DeleteAllAsync(input);
    }
}