using HC.Shared;
using HC.WorkflowTemplates;
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
using HC.WorkflowStepTemplates;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.WorkflowStepTemplates;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.WorkflowStepTemplates.Default)]
public abstract class WorkflowStepTemplatesAppServiceBase : HCAppService
{
    protected IDistributedCache<WorkflowStepTemplateDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWorkflowStepTemplateRepository _workflowStepTemplateRepository;
    protected WorkflowStepTemplateManager _workflowStepTemplateManager;
    protected IRepository<HC.WorkflowTemplates.WorkflowTemplate, Guid> _workflowTemplateRepository;

    public WorkflowStepTemplatesAppServiceBase(IWorkflowStepTemplateRepository workflowStepTemplateRepository, WorkflowStepTemplateManager workflowStepTemplateManager, IDistributedCache<WorkflowStepTemplateDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.WorkflowTemplates.WorkflowTemplate, Guid> workflowTemplateRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _workflowStepTemplateManager = workflowStepTemplateManager;
        _workflowTemplateRepository = workflowTemplateRepository;
    }

    public virtual async Task<PagedResultDto<WorkflowStepTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepTemplatesInput input)
    {
        var totalCount = await _workflowStepTemplateRepository.GetCountAsync(input.FilterText, input.OrderMin, input.OrderMax, input.Name, input.Type, input.SLADaysMin, input.SLADaysMax, input.IsActive, input.WorkflowTemplateId);
        var items = await _workflowStepTemplateRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.OrderMin, input.OrderMax, input.Name, input.Type, input.SLADaysMin, input.SLADaysMax, input.IsActive, input.WorkflowTemplateId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<WorkflowStepTemplateWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<WorkflowStepTemplateWithNavigationProperties>, List<WorkflowStepTemplateWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<WorkflowStepTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<WorkflowStepTemplateWithNavigationProperties, WorkflowStepTemplateWithNavigationPropertiesDto>(await _workflowStepTemplateRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<WorkflowStepTemplateDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<WorkflowStepTemplate, WorkflowStepTemplateDto>(await _workflowStepTemplateRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowTemplateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.WorkflowTemplates.WorkflowTemplate>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.WorkflowTemplates.WorkflowTemplate>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.WorkflowStepTemplates.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _workflowStepTemplateRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.WorkflowStepTemplates.Create)]
    public virtual async Task<WorkflowStepTemplateDto> CreateAsync(WorkflowStepTemplateCreateDto input)
    {
        if (input.WorkflowTemplateId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowTemplate"]]);
        }

        var workflowStepTemplate = await _workflowStepTemplateManager.CreateAsync(input.WorkflowTemplateId, input.Order, input.Name, input.Type, input.AllowReturn, input.IsActive, input.SLADays);
        return ObjectMapper.Map<WorkflowStepTemplate, WorkflowStepTemplateDto>(workflowStepTemplate);
    }

    [Authorize(HCPermissions.WorkflowStepTemplates.Edit)]
    public virtual async Task<WorkflowStepTemplateDto> UpdateAsync(Guid id, WorkflowStepTemplateUpdateDto input)
    {
        if (input.WorkflowTemplateId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowTemplate"]]);
        }

        var workflowStepTemplate = await _workflowStepTemplateManager.UpdateAsync(id, input.WorkflowTemplateId, input.Order, input.Name, input.Type, input.AllowReturn, input.IsActive, input.SLADays, input.ConcurrencyStamp);
        return ObjectMapper.Map<WorkflowStepTemplate, WorkflowStepTemplateDto>(workflowStepTemplate);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepTemplateExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var workflowStepTemplates = await _workflowStepTemplateRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.OrderMin, input.OrderMax, input.Name, input.Type, input.SLADaysMin, input.SLADaysMax, input.IsActive, input.WorkflowTemplateId);
        var items = workflowStepTemplates.Select(item => new { Order = item.WorkflowStepTemplate.Order, Name = item.WorkflowStepTemplate.Name, Type = item.WorkflowStepTemplate.Type, SLADays = item.WorkflowStepTemplate.SLADays, AllowReturn = item.WorkflowStepTemplate.AllowReturn, IsActive = item.WorkflowStepTemplate.IsActive, WorkflowTemplate = item.WorkflowTemplate?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "WorkflowStepTemplates.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.WorkflowStepTemplates.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> workflowsteptemplateIds)
    {
        await _workflowStepTemplateRepository.DeleteManyAsync(workflowsteptemplateIds);
    }

    [Authorize(HCPermissions.WorkflowStepTemplates.Delete)]
    public virtual async Task DeleteAllAsync(GetWorkflowStepTemplatesInput input)
    {
        await _workflowStepTemplateRepository.DeleteAllAsync(input.FilterText, input.OrderMin, input.OrderMax, input.Name, input.Type, input.SLADaysMin, input.SLADaysMax, input.IsActive, input.WorkflowTemplateId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WorkflowStepTemplateDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}