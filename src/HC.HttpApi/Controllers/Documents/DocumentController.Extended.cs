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
}