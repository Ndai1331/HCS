using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentAssignments;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Controllers.DocumentAssignments;

[RemoteService]
[Area("app")]
[ControllerName("DocumentAssignment")]
[Route("api/app56")]
public abstract class DocumentAssignmentControllerBase : AbpController
{
    protected IDocumentAssignmentsAppService _documentAssignmentsAppService;

    public DocumentAssignmentControllerBase(IDocumentAssignmentsAppService documentAssignmentsAppService)
    {
        _documentAssignmentsAppService = documentAssignmentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentAssignmentWithNavigationPropertiesDto>> GetListAsync(GetDocumentAssignmentsInput input)
    {
        return _documentAssignmentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentAssignmentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentAssignmentDto> GetAsync(Guid id)
    {
        return _documentAssignmentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        return _documentAssignmentsAppService.GetDocumentLookupAsync(input);
    }

    [HttpGet]
    [Route("workflow-step-template-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input)
    {
        return _documentAssignmentsAppService.GetWorkflowStepTemplateLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _documentAssignmentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentAssignmentDto> CreateAsync(DocumentAssignmentCreateDto input)
    {
        return _documentAssignmentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentAssignmentDto> UpdateAsync(Guid id, DocumentAssignmentUpdateDto input)
    {
        return _documentAssignmentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentAssignmentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentAssignmentExcelDownloadDto input)
    {
        return _documentAssignmentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _documentAssignmentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> documentassignmentIds)
    {
        return _documentAssignmentsAppService.DeleteByIdsAsync(documentassignmentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetDocumentAssignmentsInput input)
    {
        return _documentAssignmentsAppService.DeleteAllAsync(input);
    }
}