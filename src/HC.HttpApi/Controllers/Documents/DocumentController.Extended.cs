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

    public async Task<bool> SendDocumentAsync(SendDocumentInput input)
    {
        return await _documentsAppService.SendDocumentAsync(input);
    }
}