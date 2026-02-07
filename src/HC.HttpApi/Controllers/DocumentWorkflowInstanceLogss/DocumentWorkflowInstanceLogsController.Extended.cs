using Asp.Versioning;
using System;
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
public class DocumentWorkflowInstanceLogsController : DocumentWorkflowInstanceLogsControllerBase, IDocumentWorkflowInstanceLogssAppService
{
    public DocumentWorkflowInstanceLogsController(IDocumentWorkflowInstanceLogssAppService documentWorkflowInstanceLogssAppService) : base(documentWorkflowInstanceLogssAppService)
    {
    }
}