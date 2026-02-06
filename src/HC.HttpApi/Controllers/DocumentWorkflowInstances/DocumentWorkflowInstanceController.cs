using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentWorkflowInstances;
using Volo.Abp.Content;

namespace HC.Controllers.DocumentWorkflowInstances;

[RemoteService]
[Area("app")]
[ControllerName("DocumentWorkflowInstance")]
[Route("api/app/document-workflow-instances")]
public abstract class DocumentWorkflowInstanceControllerBase : AbpController
{
    protected IDocumentWorkflowInstancesAppService _documentWorkflowInstancesAppService;

    public DocumentWorkflowInstanceControllerBase(IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService)
    {
        _documentWorkflowInstancesAppService = documentWorkflowInstancesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstancesInput input)
    {
        return _documentWorkflowInstancesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentWorkflowInstanceWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentWorkflowInstancesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceDto> GetAsync(Guid id)
    {
        return _documentWorkflowInstancesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstancesAppService.GetDocumentLookupAsync(input);
    }

    [HttpGet]
    [Route("workflow-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowLookupAsync(input);
    }

    [HttpGet]
    [Route("workflow-template-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowTemplateLookupAsync(input);
    }

    [HttpGet]
    [Route("workflow-step-template-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowStepTemplateLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentWorkflowInstanceDto> CreateAsync(DocumentWorkflowInstanceCreateDto input)
    {
        return _documentWorkflowInstancesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceDto> UpdateAsync(Guid id, DocumentWorkflowInstanceUpdateDto input)
    {
        return _documentWorkflowInstancesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentWorkflowInstancesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentWorkflowInstanceExcelDownloadDto input)
    {
        return _documentWorkflowInstancesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _documentWorkflowInstancesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> documentworkflowinstanceIds)
    {
        return _documentWorkflowInstancesAppService.DeleteByIdsAsync(documentworkflowinstanceIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetDocumentWorkflowInstancesInput input)
    {
        return _documentWorkflowInstancesAppService.DeleteAllAsync(input);
    }
}