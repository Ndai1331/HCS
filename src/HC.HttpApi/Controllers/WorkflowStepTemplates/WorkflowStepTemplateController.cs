using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.WorkflowStepTemplates;
using Volo.Abp.Content;

namespace HC.Controllers.WorkflowStepTemplates;

[RemoteService]
[Area("app")]
[ControllerName("WorkflowStepTemplate")]
[Route("api/app/workflow-step-templates")]
public abstract class WorkflowStepTemplateControllerBase : AbpController
{
    protected IWorkflowStepTemplatesAppService _workflowStepTemplatesAppService;

    public WorkflowStepTemplateControllerBase(IWorkflowStepTemplatesAppService workflowStepTemplatesAppService)
    {
        _workflowStepTemplatesAppService = workflowStepTemplatesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<WorkflowStepTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepTemplatesInput input)
    {
        return _workflowStepTemplatesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<WorkflowStepTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _workflowStepTemplatesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<WorkflowStepTemplateDto> GetAsync(Guid id)
    {
        return _workflowStepTemplatesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("workflow-template-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input)
    {
        return _workflowStepTemplatesAppService.GetWorkflowTemplateLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<WorkflowStepTemplateDto> CreateAsync(WorkflowStepTemplateCreateDto input)
    {
        return _workflowStepTemplatesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<WorkflowStepTemplateDto> UpdateAsync(Guid id, WorkflowStepTemplateUpdateDto input)
    {
        return _workflowStepTemplatesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _workflowStepTemplatesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepTemplateExcelDownloadDto input)
    {
        return _workflowStepTemplatesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _workflowStepTemplatesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> workflowsteptemplateIds)
    {
        return _workflowStepTemplatesAppService.DeleteByIdsAsync(workflowsteptemplateIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetWorkflowStepTemplatesInput input)
    {
        return _workflowStepTemplatesAppService.DeleteAllAsync(input);
    }
}