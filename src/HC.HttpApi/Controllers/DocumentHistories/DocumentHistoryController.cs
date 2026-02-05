using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentHistories;
using Volo.Abp.Content;
using Microsoft.Extensions.Logging;

namespace HC.Controllers.DocumentHistories;

[RemoteService]
[Area("app")]
[ControllerName("DocumentHistory")]
[Route("api/app/document-histories")]
public abstract class DocumentHistoryControllerBase : AbpController
{
    protected IDocumentHistoriesAppService _documentHistoriesAppService;
    protected ILogger<DocumentHistoryControllerBase> Logger;
    public DocumentHistoryControllerBase(IDocumentHistoriesAppService documentHistoriesAppService, ILogger<DocumentHistoryControllerBase> logger)
    {
        _documentHistoriesAppService = documentHistoriesAppService;
        Logger = logger;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetListAsync(GetDocumentHistoriesInput input)
    {
        Logger.LogInformation($"GetListAsync called with input: {input}");
        return _documentHistoriesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<DocumentHistoryWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        Logger.LogInformation($"GetWithNavigationPropertiesAsync called with id: {id}");
        return _documentHistoriesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<DocumentHistoryDto> GetAsync(Guid id)
    {
        return _documentHistoriesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("document-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        return _documentHistoriesAppService.GetDocumentLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _documentHistoriesAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<DocumentHistoryDto> CreateAsync(DocumentHistoryCreateDto input)
    {
        return _documentHistoriesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<DocumentHistoryDto> UpdateAsync(Guid id, DocumentHistoryUpdateDto input)
    {
        return _documentHistoriesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _documentHistoriesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentHistoryExcelDownloadDto input)
    {
        return _documentHistoriesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _documentHistoriesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> documenthistoryIds)
    {
        return _documentHistoriesAppService.DeleteByIdsAsync(documenthistoryIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetDocumentHistoriesInput input)
    {
        return _documentHistoriesAppService.DeleteAllAsync(input);
    }

    [HttpPost]
    [Route("by-document-id")]
    public async Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetHistoryByDocumentIdAsync(GetDocumentHistoriesInput input)
    {
        if (input.DocumentId == null)
        {
            throw new UserFriendlyException("DocumentId is required");
        }
        Logger.LogInformation($"GetHistoryByDocumentIdAsync called with documentId: {input.DocumentId.Value}, skipCount: {input.SkipCount}, maxResultCount: {input.MaxResultCount}");
        return await _documentHistoriesAppService.GetHistoryByDocumentIdAsync(input);
    }
}


[RemoteService]
[Area("app")]
[ControllerName("DocumentHistory")]
[Route("api/app/document-histories")]
public class DocumentHistoryController : DocumentHistoryControllerBase, IDocumentHistoriesAppService
{
    public DocumentHistoryController(
        IDocumentHistoriesAppService documentHistoriesAppService,
        ILogger<DocumentHistoryControllerBase> logger) 
        : base(documentHistoriesAppService, logger)
    {
    }
}