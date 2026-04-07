using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.Documents;
using Volo.Abp.Content;

namespace HC.Controllers.Documents;

[RemoteService]
[Area("app")]
[ControllerName("Document")]
[Route("api/app/documents")]
public abstract class DocumentControllerBase : AbpController
{
    protected IDocumentsAppService _documentsAppService;

    public DocumentControllerBase(IDocumentsAppService documentsAppService)
    {
        _documentsAppService = documentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentWithNavigationPropertiesDto>> GetListAsync(GetDocumentsInput input)
    {
        return _documentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentDto> GetAsync(Guid id)
    {
        return _documentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("master-data-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetMasterDataLookupAsync(LookupRequestDto input)
    {
        return _documentsAppService.GetMasterDataLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _documentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpGet]
    [Route("unit-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetUnitLookupAsync(LookupRequestDto input)
    {
        return _documentsAppService.GetUnitLookupAsync(input);
    }

    [HttpGet]
    [Route("unit-lookup/{id}")]
    public virtual Task<LookupDto<Guid>?> GetUnitLookupByIdAsync(Guid id)
    {
        return _documentsAppService.GetUnitLookupByIdAsync(id);
    }

    [HttpGet]
    [Route("workflow-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        return _documentsAppService.GetWorkflowLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentDto> CreateAsync(DocumentCreateDto input)
    {
        return _documentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input)
    {
        return _documentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentExcelDownloadDto input)
    {
        return _documentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _documentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> documentIds)
    {
        return _documentsAppService.DeleteByIdsAsync(documentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetDocumentsInput input)
    {
        return _documentsAppService.DeleteAllAsync(input);
    }
}