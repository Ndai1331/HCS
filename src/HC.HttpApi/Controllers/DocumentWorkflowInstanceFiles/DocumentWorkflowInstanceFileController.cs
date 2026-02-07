using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentWorkflowInstanceFiles;

namespace HC.Controllers.DocumentWorkflowInstanceFiles;

[RemoteService]
[Area("app")]
[ControllerName("DocumentWorkflowInstanceFile")]
[Route("api/app/document-workflow-instance-files")]
public abstract class DocumentWorkflowInstanceFileControllerBase : AbpController
{
    protected IDocumentWorkflowInstanceFilesAppService _documentWorkflowInstanceFilesAppService;

    public DocumentWorkflowInstanceFileControllerBase(IDocumentWorkflowInstanceFilesAppService documentWorkflowInstanceFilesAppService)
    {
        _documentWorkflowInstanceFilesAppService = documentWorkflowInstanceFilesAppService;
    }

    [HttpGet]
    [Route("by-document-workflow-instance")]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceFileDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input)
    {
        return _documentWorkflowInstanceFilesAppService.GetListByDocumentWorkflowInstanceIdAsync(input);
    }

    [HttpGet]
    [Route("detailed/by-document-workflow-instance")]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input)
    {
        return _documentWorkflowInstanceFilesAppService.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(input);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceFilesInput input)
    {
        return _documentWorkflowInstanceFilesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentWorkflowInstanceFilesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceFileDto> GetAsync(Guid id)
    {
        return _documentWorkflowInstanceFilesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-file-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentFileLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstanceFilesAppService.GetDocumentFileLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentWorkflowInstanceFileDto> CreateAsync(DocumentWorkflowInstanceFileCreateDto input)
    {
        return _documentWorkflowInstanceFilesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceFileDto> UpdateAsync(Guid id, DocumentWorkflowInstanceFileUpdateDto input)
    {
        return _documentWorkflowInstanceFilesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentWorkflowInstanceFilesAppService.DeleteAsync(id);
    }
}