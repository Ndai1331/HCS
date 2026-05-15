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
using HC.WorkflowDefinitions;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.WorkflowDefinitions;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.WorkflowDefinitions.Default)]
public abstract class WorkflowDefinitionsAppServiceBase : HCAppService
{
    protected IDistributedCache<WorkflowDefinitionDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWorkflowDefinitionRepository _workflowDefinitionRepository;
    protected WorkflowDefinitionManager _workflowDefinitionManager;

    public WorkflowDefinitionsAppServiceBase(IWorkflowDefinitionRepository workflowDefinitionRepository, WorkflowDefinitionManager workflowDefinitionManager, IDistributedCache<WorkflowDefinitionDownloadTokenCacheItem, string> downloadTokenCache)
    {
        _downloadTokenCache = downloadTokenCache;
        _workflowDefinitionRepository = workflowDefinitionRepository;
        _workflowDefinitionManager = workflowDefinitionManager;
    }

    public virtual async Task<PagedResultDto<WorkflowDefinitionDto>> GetListAsync(GetWorkflowDefinitionsInput input)
    {
        var totalCount = await _workflowDefinitionRepository.GetCountAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
        var items = await _workflowDefinitionRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<WorkflowDefinitionDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<WorkflowDefinition>, List<WorkflowDefinitionDto>>(items)
        };
    }

    public virtual async Task<WorkflowDefinitionDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<WorkflowDefinition, WorkflowDefinitionDto>(await _workflowDefinitionRepository.GetAsync(id));
    }

    [Authorize(HCPermissions.WorkflowDefinitions.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _workflowDefinitionRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.WorkflowDefinitions.Create)]
    public virtual async Task<WorkflowDefinitionDto> CreateAsync(WorkflowDefinitionCreateDto input)
    {
        var workflowDefinition = await _workflowDefinitionManager.CreateAsync(input.Code, input.Name, input.IsActive, input.Description);
        return ObjectMapper.Map<WorkflowDefinition, WorkflowDefinitionDto>(workflowDefinition);
    }

    [Authorize(HCPermissions.WorkflowDefinitions.Edit)]
    public virtual async Task<WorkflowDefinitionDto> UpdateAsync(Guid id, WorkflowDefinitionUpdateDto input)
    {
        var workflowDefinition = await _workflowDefinitionManager.UpdateAsync(id, input.Code, input.Name, input.IsActive, input.Description, input.ConcurrencyStamp);
        return ObjectMapper.Map<WorkflowDefinition, WorkflowDefinitionDto>(workflowDefinition);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowDefinitionExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var items = await _workflowDefinitionRepository.GetListAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(ObjectMapper.Map<List<WorkflowDefinition>, List<WorkflowDefinitionExcelDto>>(items));
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "WorkflowDefinitions.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.WorkflowDefinitions.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> workflowdefinitionIds)
    {
        await _workflowDefinitionRepository.DeleteManyAsync(workflowdefinitionIds);
    }

    [Authorize(HCPermissions.WorkflowDefinitions.Delete)]
    public virtual async Task DeleteAllAsync(GetWorkflowDefinitionsInput input)
    {
        await _workflowDefinitionRepository.DeleteAllAsync(input.FilterText, input.Code, input.Name, input.Description, input.IsActive);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WorkflowDefinitionDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}