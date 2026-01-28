using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.ProjectTaskDocuments;
using Volo.Abp.Content;

namespace HC.Controllers.ProjectTaskDocuments;

[RemoteService]
[Area("app")]
[ControllerName("ProjectTaskDocument")]
[Route("api/app/project-task-documents")]
public abstract class ProjectTaskDocumentControllerBase : AbpController
{
    protected IProjectTaskDocumentsAppService _projectTaskDocumentsAppService;

    public ProjectTaskDocumentControllerBase(IProjectTaskDocumentsAppService projectTaskDocumentsAppService)
    {
        _projectTaskDocumentsAppService = projectTaskDocumentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<ProjectTaskDocumentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskDocumentsInput input)
    {
        return _projectTaskDocumentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<ProjectTaskDocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _projectTaskDocumentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<ProjectTaskDocumentDto> GetAsync(Guid id)
    {
        return _projectTaskDocumentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("project-task-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input)
    {
        return _projectTaskDocumentsAppService.GetProjectTaskLookupAsync(input);
    }

    [HttpGet]
    [Route("document-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        return _projectTaskDocumentsAppService.GetDocumentLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<ProjectTaskDocumentDto> CreateAsync(ProjectTaskDocumentCreateDto input)
    {
        return _projectTaskDocumentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<ProjectTaskDocumentDto> UpdateAsync(Guid id, ProjectTaskDocumentUpdateDto input)
    {
        return _projectTaskDocumentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _projectTaskDocumentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskDocumentExcelDownloadDto input)
    {
        return _projectTaskDocumentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _projectTaskDocumentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> projecttaskdocumentIds)
    {
        return _projectTaskDocumentsAppService.DeleteByIdsAsync(projecttaskdocumentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetProjectTaskDocumentsInput input)
    {
        return _projectTaskDocumentsAppService.DeleteAllAsync(input);
    }
}