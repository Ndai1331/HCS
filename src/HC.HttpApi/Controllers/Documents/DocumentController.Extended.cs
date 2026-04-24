using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.Documents;

namespace HC.Controllers.Documents;

[RemoteService]
[Area("app")]
[ControllerName("Document")]
[Route("api/app/documents")]
public class DocumentController : DocumentControllerBase, IDocumentsAppService
{
    public DocumentController(IDocumentsAppService documentsAppService) : base(documentsAppService)
    {
    }

    [HttpPost]
    [Route("send-document")]
    public virtual Task<bool> SendDocumentAsync(SendDocumentInput input)
    {
        return _documentsAppService.SendDocumentAsync(input);
    }

    [HttpPost]
    [Route("revoke-document")]
    public virtual Task<bool> RevokeDocumentAsync(RevokeDocumentInput input)
    {
        return _documentsAppService.RevokeDocumentAsync(input);
    }

    [HttpPost]
    [Route("submit-for-approval")]
    public virtual Task<bool> SubmitForApprovalAsync(SubmitDocumentForApprovalInput input)
    {
        return _documentsAppService.SubmitForApprovalAsync(input);
    }

    [HttpPost]
    [Route("reject-approval")]
    public virtual Task<bool> RejectApprovalAsync(RejectDocumentApprovalInput input)
    {
        return _documentsAppService.RejectApprovalAsync(input);
    }

    [HttpPost]
    [Route("approve-with-note")]
    public virtual Task<bool> ApproveWithNoteAsync(ApproveDocumentWithNoteInput input)
    {
        return _documentsAppService.ApproveWithNoteAsync(input);
    }

    [HttpGet]
    [Route("check-duplicate-document-number")]
    public virtual Task<bool> IsDocumentNumberDuplicateAsync(string no, Guid? excludeDocumentId = null)
    {
        return _documentsAppService.IsDocumentNumberDuplicateAsync(no, excludeDocumentId);
    }

    [HttpGet]
    [Route("check-duplicate-storage-number")]
    public virtual Task<bool> IsStorageNumberDuplicateAsync(string storageNumber, Guid? excludeDocumentId = null)
    {
        return _documentsAppService.IsStorageNumberDuplicateAsync(storageNumber, excludeDocumentId);
    }

    [HttpGet]
    [Route("detail-bundle")]
    public virtual Task<DocumentDetailBundleDto> GetDetailBundleAsync(GetDocumentDetailBundleInput input)
    {
        return _documentsAppService.GetDetailBundleAsync(input);
    }

    [HttpPost]
    [Route("page-bootstrap")]
    public virtual Task<DocumentsPageBootstrapDto> GetPageBootstrapAsync(GetDocumentsPageBootstrapInput input)
    {
        return _documentsAppService.GetPageBootstrapAsync(input);
    }
}