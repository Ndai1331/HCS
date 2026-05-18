using HC.Shared;
using HC.WorkflowDefinitions;
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
using HC.Workflows;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using Volo.Abp.Domain.Entities;

namespace HC.Workflows;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.Workflows.Default)]
public abstract class WorkflowsAppServiceBase : HCAppService
{
    protected IDistributedCache<WorkflowDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWorkflowRepository _workflowRepository;
    protected WorkflowManager _workflowManager;
    protected IRepository<HC.WorkflowDefinitions.WorkflowDefinition, Guid> _workflowDefinitionRepository;
    protected IDistributedCache<LookupCacheVersionCacheItem, string> _lookupVersionCache;

    public WorkflowsAppServiceBase(IWorkflowRepository workflowRepository, WorkflowManager workflowManager, IDistributedCache<WorkflowDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.WorkflowDefinitions.WorkflowDefinition, Guid> workflowDefinitionRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _workflowRepository = workflowRepository;
        _workflowManager = workflowManager;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _lookupVersionCache = LazyServiceProvider.LazyGetRequiredService<IDistributedCache<LookupCacheVersionCacheItem, string>>();
    }

    public virtual async Task<PagedResultDto<WorkflowWithNavigationPropertiesDto>> GetListAsync(GetWorkflowsInput input)
    {
        var totalCount = await _workflowRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.WorkflowDefinitionId);
        var items = await _workflowRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.WorkflowDefinitionId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<WorkflowWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<WorkflowWithNavigationProperties>, List<WorkflowWithNavigationPropertiesDto>>(items ?? new List<WorkflowWithNavigationProperties>())
        };
    }

    public virtual async Task<WorkflowWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var entity = await _workflowRepository.GetWithNavigationPropertiesAsync(id);
        if (entity == null)
        {
            throw new EntityNotFoundException(typeof(Workflow), id);
        }

        return ObjectMapper.Map<WorkflowWithNavigationProperties, WorkflowWithNavigationPropertiesDto>(entity);
    }

    public virtual async Task<WorkflowDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<Workflow, WorkflowDto>(await _workflowRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowDefinitionLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowDefinitionRepository.GetQueryableAsync())
        .WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)))
        .WhereIf(input.IsActive.HasValue, x => x.IsActive == input.IsActive);
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.WorkflowDefinitions.WorkflowDefinition>();
        var rows = lookupData ?? new List<HC.WorkflowDefinitions.WorkflowDefinition>();
        var totalCount = (long)await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.WorkflowDefinitions.WorkflowDefinition>, List<LookupDto<Guid>>>(rows)
        };
    }

    [Authorize(HCPermissions.Workflows.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _workflowRepository.DeleteAsync(id);
        await InvalidateWorkflowLookupCacheAsync();
    }

    [Authorize(HCPermissions.Workflows.Create)]
    public virtual async Task<WorkflowDto> CreateAsync(WorkflowCreateDto input)
    {
        if (input.WorkflowDefinitionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowDefinition"]]);
        }

        var workflow = await _workflowManager.CreateAsync(input.WorkflowDefinitionId, input.Code, input.Name, input.IsActive, input.Description);
        await InvalidateWorkflowLookupCacheAsync();
        return ObjectMapper.Map<Workflow, WorkflowDto>(workflow);
    }

    [Authorize(HCPermissions.Workflows.Edit)]
    public virtual async Task<WorkflowDto> UpdateAsync(Guid id, WorkflowUpdateDto input)
    {
        if (input.WorkflowDefinitionId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowDefinition"]]);
        }

        var workflow = await _workflowManager.UpdateAsync(id, input.WorkflowDefinitionId, input.Code, input.Name, input.IsActive, input.Description, input.ConcurrencyStamp);
        await InvalidateWorkflowLookupCacheAsync();
        return ObjectMapper.Map<Workflow, WorkflowDto>(workflow);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var workflows = await _workflowRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.WorkflowDefinitionId);
        var items = workflows.Select(item => new { Code = item.Workflow.Code, Name = item.Workflow.Name, Description = item.Workflow.Description, IsActive = item.Workflow.IsActive, WorkflowDefinition = item.WorkflowDefinition?.Code, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "Workflows.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.Workflows.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> workflowIds)
    {
        await _workflowRepository.DeleteManyAsync(workflowIds);
        await InvalidateWorkflowLookupCacheAsync();
    }

    [Authorize(HCPermissions.Workflows.Delete)]
    public virtual async Task DeleteAllAsync(GetWorkflowsInput input)
    {
        await _workflowRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.WorkflowDefinitionId);
        await InvalidateWorkflowLookupCacheAsync();
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WorkflowDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }

    protected virtual async Task InvalidateWorkflowLookupCacheAsync()
    {
        var current = await _lookupVersionCache.GetAsync("lookup-version:workflow");
        await _lookupVersionCache.SetAsync(
            "lookup-version:workflow",
            new LookupCacheVersionCacheItem { Version = (current?.Version ?? 1) + 1 },
            new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
    }
}
