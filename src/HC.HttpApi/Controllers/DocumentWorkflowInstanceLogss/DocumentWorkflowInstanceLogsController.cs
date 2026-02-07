using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentWorkflowInstanceLogss;

namespace HC.Controllers.DocumentWorkflowInstanceLogss;

[RemoteService]
[Area("app")]
[ControllerName("DocumentWorkflowInstanceLogs")]
[Route("api/app/document-workflow-instance-logss")]
public abstract class DocumentWorkflowInstanceLogsControllerBase : AbpController
{
    protected IDocumentWorkflowInstanceLogssAppService _documentWorkflowInstanceLogssAppService;

    public DocumentWorkflowInstanceLogsControllerBase(IDocumentWorkflowInstanceLogssAppService documentWorkflowInstanceLogssAppService)
    {
        _documentWorkflowInstanceLogssAppService = documentWorkflowInstanceLogssAppService;
    }

    [HttpGet]
    [Route("by-document-workflow-instance")]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceLogsDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input)
    {
        return _documentWorkflowInstanceLogssAppService.GetListByDocumentWorkflowInstanceIdAsync(input);
    }

    [HttpGet]
    [Route("detailed/by-document-workflow-instance")]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input)
    {
        return _documentWorkflowInstanceLogssAppService.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(input);
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceLogssInput input)
    {
        return _documentWorkflowInstanceLogssAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentWorkflowInstanceLogssAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceLogsDto> GetAsync(Guid id)
    {
        return _documentWorkflowInstanceLogssAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-assignment-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentAssignmentLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstanceLogssAppService.GetDocumentAssignmentLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _documentWorkflowInstanceLogssAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentWorkflowInstanceLogsDto> CreateAsync(DocumentWorkflowInstanceLogsCreateDto input)
    {
        return _documentWorkflowInstanceLogssAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentWorkflowInstanceLogsDto> UpdateAsync(Guid id, DocumentWorkflowInstanceLogsUpdateDto input)
    {
        return _documentWorkflowInstanceLogssAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentWorkflowInstanceLogssAppService.DeleteAsync(id);
    }
}