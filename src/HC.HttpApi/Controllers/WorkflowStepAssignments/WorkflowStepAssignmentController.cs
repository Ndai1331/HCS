using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.WorkflowStepAssignments;
using Volo.Abp.Content;

namespace HC.Controllers.WorkflowStepAssignments;

[RemoteService]
[Area("app")]
[ControllerName("WorkflowStepAssignment")]
[Route("api/app/workflow-step-assignments")]
public abstract class WorkflowStepAssignmentControllerBase : AbpController
{
    protected IWorkflowStepAssignmentsAppService _workflowStepAssignmentsAppService;

    public WorkflowStepAssignmentControllerBase(IWorkflowStepAssignmentsAppService workflowStepAssignmentsAppService)
    {
        _workflowStepAssignmentsAppService = workflowStepAssignmentsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<WorkflowStepAssignmentWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepAssignmentsInput input)
    {
        return _workflowStepAssignmentsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<WorkflowStepAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _workflowStepAssignmentsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<WorkflowStepAssignmentDto> GetAsync(Guid id)
    {
        return _workflowStepAssignmentsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("workflow-step-template-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input)
    {
        return _workflowStepAssignmentsAppService.GetWorkflowStepTemplateLookupAsync(input);
    }

    [HttpGet]
    [Route("identity-user-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        return _workflowStepAssignmentsAppService.GetIdentityUserLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<WorkflowStepAssignmentDto> CreateAsync(WorkflowStepAssignmentCreateDto input)
    {
        return _workflowStepAssignmentsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<WorkflowStepAssignmentDto> UpdateAsync(Guid id, WorkflowStepAssignmentUpdateDto input)
    {
        return _workflowStepAssignmentsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _workflowStepAssignmentsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepAssignmentExcelDownloadDto input)
    {
        return _workflowStepAssignmentsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _workflowStepAssignmentsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> workflowstepassignmentIds)
    {
        return _workflowStepAssignmentsAppService.DeleteByIdsAsync(workflowstepassignmentIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetWorkflowStepAssignmentsInput input)
    {
        return _workflowStepAssignmentsAppService.DeleteAllAsync(input);
    }
}