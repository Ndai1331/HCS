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
}