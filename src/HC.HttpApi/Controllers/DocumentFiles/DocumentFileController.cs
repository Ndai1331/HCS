using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentFiles;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Controllers.DocumentFiles;

[RemoteService]
[Area("app")]
[ControllerName("DocumentFile")]
[Route("api/app/document-files")]
public abstract class DocumentFileControllerBase : AbpController
{
    protected IDocumentFilesAppService _documentFilesAppService;

    public DocumentFileControllerBase(IDocumentFilesAppService documentFilesAppService)
    {
        _documentFilesAppService = documentFilesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentFilesInput input)
    {
        return _documentFilesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _documentFilesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentFileDto> GetAsync(Guid id)
    {
        return _documentFilesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        return _documentFilesAppService.GetDocumentLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentFileDto> CreateAsync(DocumentFileCreateDto input)
    {
        return _documentFilesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentFileDto> UpdateAsync(Guid id, DocumentFileUpdateDto input)
    {
        return _documentFilesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentFilesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentFileExcelDownloadDto input)
    {
        return _documentFilesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _documentFilesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> documentfileIds)
    {
        return _documentFilesAppService.DeleteByIdsAsync(documentfileIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetDocumentFilesInput input)
    {
        return _documentFilesAppService.DeleteAllAsync(input);
    }
}