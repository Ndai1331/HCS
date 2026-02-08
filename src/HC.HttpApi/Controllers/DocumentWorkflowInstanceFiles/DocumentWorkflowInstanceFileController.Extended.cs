using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentWorkflowInstanceFiles;

namespace HC.Controllers.DocumentWorkflowInstanceFiles;

[RemoteService]
[Area("app")]
[ControllerName("DocumentWorkflowInstanceFile")]
[Route("api/app/document-workflow-instance-files")]
public class DocumentWorkflowInstanceFileController : DocumentWorkflowInstanceFileControllerBase, IDocumentWorkflowInstanceFilesAppService
{
    public DocumentWorkflowInstanceFileController(IDocumentWorkflowInstanceFilesAppService documentWorkflowInstanceFilesAppService) : base(documentWorkflowInstanceFilesAppService)
    {
    }
}