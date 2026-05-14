using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.WorkflowTemplates;
using Volo.Abp.Content;

namespace HC.Controllers.WorkflowTemplates;

[RemoteService]
[Area("app")]
[ControllerName("WorkflowTemplate")]
[Route("api/app/workflow-templates")]
public abstract class WorkflowTemplateControllerBase : AbpController
{
    protected IWorkflowTemplatesAppService _workflowTemplatesAppService;

    public WorkflowTemplateControllerBase(IWorkflowTemplatesAppService workflowTemplatesAppService)
    {
        _workflowTemplatesAppService = workflowTemplatesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<WorkflowTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowTemplatesInput input)
    {
        return _workflowTemplatesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<WorkflowTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _workflowTemplatesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<WorkflowTemplateDto> GetAsync(Guid id)
    {
        return _workflowTemplatesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("workflow-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        return _workflowTemplatesAppService.GetWorkflowLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<WorkflowTemplateDto> CreateAsync(WorkflowTemplateCreateDto input)
    {
        return _workflowTemplatesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<WorkflowTemplateDto> UpdateAsync(Guid id, WorkflowTemplateUpdateDto input)
    {
        return _workflowTemplatesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _workflowTemplatesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowTemplateExcelDownloadDto input)
    {
        return _workflowTemplatesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _workflowTemplatesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> workflowtemplateIds)
    {
        return _workflowTemplatesAppService.DeleteByIdsAsync(workflowtemplateIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetWorkflowTemplatesInput input)
    {
        return _workflowTemplatesAppService.DeleteAllAsync(input);
    }
}