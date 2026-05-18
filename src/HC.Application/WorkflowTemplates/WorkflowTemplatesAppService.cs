using HC.Shared;
using HC.Workflows;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq.Dynamic.Core;
using Microsoft.AspNetCore.Authorization;
using Volo.Abp;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Domain.Repositories;
using HC.Permissions;
using HC.WorkflowTemplates;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.EntityFrameworkCore;

namespace HC.WorkflowTemplates;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.WorkflowTemplates.Default)]
public abstract class WorkflowTemplatesAppServiceBase : HCAppService
{
    protected IDistributedCache<WorkflowTemplateDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWorkflowTemplateRepository _workflowTemplateRepository;
    protected WorkflowTemplateManager _workflowTemplateManager;
    protected IRepository<HC.Workflows.Workflow, Guid> _workflowRepository;

    public WorkflowTemplatesAppServiceBase(IWorkflowTemplateRepository workflowTemplateRepository, WorkflowTemplateManager workflowTemplateManager, IDistributedCache<WorkflowTemplateDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Workflows.Workflow, Guid> workflowRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowTemplateManager = workflowTemplateManager;
        _workflowRepository = workflowRepository;
    }

    public virtual async Task<PagedResultDto<WorkflowTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowTemplatesInput input)
    {
        var totalCount = await _workflowTemplateRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.OutputFormat, input.WorkflowId);
        var items = await _workflowTemplateRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.OutputFormat, input.WorkflowId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<WorkflowTemplateWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<WorkflowTemplateWithNavigationProperties>, List<WorkflowTemplateWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<WorkflowTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<WorkflowTemplateWithNavigationProperties, WorkflowTemplateWithNavigationPropertiesDto>(await _workflowTemplateRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<WorkflowTemplateDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<WorkflowTemplate, WorkflowTemplateDto>(await _workflowTemplateRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Workflows.Workflow>();
        var totalCount = await query.CountAsync();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Workflows.Workflow>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.WorkflowTemplates.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _workflowTemplateRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.WorkflowTemplates.Create)]
    public virtual async Task<WorkflowTemplateDto> CreateAsync(WorkflowTemplateCreateDto input)
    {
        if (input.WorkflowId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);
        }

        var workflowTemplate = await _workflowTemplateManager.CreateAsync(input.WorkflowId, input.Code, input.Name, input.WordTemplatePath, input.PdfTemplatePath, input.ContentSchema, input.OutputFormat, input.SignMode);
        return ObjectMapper.Map<WorkflowTemplate, WorkflowTemplateDto>(workflowTemplate);
    }

    [Authorize(HCPermissions.WorkflowTemplates.Edit)]
    public virtual async Task<WorkflowTemplateDto> UpdateAsync(Guid id, WorkflowTemplateUpdateDto input)
    {
        if (input.WorkflowId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);
        }

        var workflowTemplate = await _workflowTemplateManager.UpdateAsync(id, input.WorkflowId, input.Code, input.Name, input.WordTemplatePath, input.PdfTemplatePath, input.ContentSchema, input.OutputFormat, input.SignMode, input.ConcurrencyStamp);
        return ObjectMapper.Map<WorkflowTemplate, WorkflowTemplateDto>(workflowTemplate);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowTemplateExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var workflowTemplates = await _workflowTemplateRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.OutputFormat, input.WorkflowId);
        var items = workflowTemplates.Select(item => new { Code = item.WorkflowTemplate.Code, Name = item.WorkflowTemplate.Name, WordTemplatePath = item.WorkflowTemplate.WordTemplatePath, ContentSchema = item.WorkflowTemplate.ContentSchema, OutputFormat = item.WorkflowTemplate.OutputFormat, SignMode = item.WorkflowTemplate.SignMode, Workflow = item.Workflow?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "WorkflowTemplates.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.WorkflowTemplates.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> workflowtemplateIds)
    {
        await _workflowTemplateRepository.DeleteManyAsync(workflowtemplateIds);
    }

    [Authorize(HCPermissions.WorkflowTemplates.Delete)]
    public virtual async Task DeleteAllAsync(GetWorkflowTemplatesInput input)
    {
        await _workflowTemplateRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.OutputFormat, input.WorkflowId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WorkflowTemplateDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}