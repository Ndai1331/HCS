using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.DocumentWorkflowInstances;
using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentHistories;
using HC.Shared;

namespace HC.Controllers.DocumentWorkflowInstances;

[RemoteService]
[Area("app")]
[ControllerName("DocumentWorkflowInstance")]
[Route("api/app/document-workflow-instances")]
public class DocumentWorkflowInstanceController : DocumentWorkflowInstanceControllerBase, IDocumentWorkflowInstancesAppService
{
    public DocumentWorkflowInstanceController(IDocumentWorkflowInstancesAppService documentWorkflowInstancesAppService) : base(documentWorkflowInstancesAppService)
    {
    }

    [HttpGet]
    [Route("workflow-submit-info/{workflowId}")]
    public Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowSubmitInfoAsync(workflowId);
    }

    [HttpPost]
    [Route("submit-to-workflow")]
    public Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input)
    {
        return _documentWorkflowInstancesAppService.SubmitToWorkflowAsync(input);
    }

    [HttpPost]
    [Route("process-workflow-action")]
    public Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input)
    {
        return _documentWorkflowInstancesAppService.ProcessWorkflowActionAsync(input);
    }

    [HttpGet]
    [Route("active-workflow-status/{documentId}")]
    public Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId)
    {
        return _documentWorkflowInstancesAppService.GetActiveWorkflowStatusAsync(documentId);
    }

    [HttpGet]
    [Route("document-signing-list")]
    public Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync([FromQuery] GetDocumentSigningListInput input)
    {
        return _documentWorkflowInstancesAppService.GetDocumentSigningListAsync(input);
    }

    [HttpGet]
    [Route("workflow-instance-logs/{workflowInstanceId}")]
    public Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(Guid workflowInstanceId)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowInstanceLogsAsync(workflowInstanceId);
    }

    [HttpGet]
    [Route("workflow-instance-files/{workflowInstanceId}")]
    public Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(Guid workflowInstanceId)
    {
        return _documentWorkflowInstancesAppService.GetWorkflowInstanceFilesAsync(workflowInstanceId);
    }

    [HttpGet]
    [Route("document-histories/{documentId}")]
    public Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(Guid documentId)
    {
        return _documentWorkflowInstancesAppService.GetDocumentHistoriesByDocumentIdAsync(documentId);
    }
}
