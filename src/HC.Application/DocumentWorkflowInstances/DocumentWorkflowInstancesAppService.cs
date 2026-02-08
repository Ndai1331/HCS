using HC.Shared;
using HC.WorkflowStepTemplates;
using HC.WorkflowTemplates;
using HC.Workflows;
using HC.Documents;
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
using HC.DocumentWorkflowInstances;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.DocumentWorkflowInstances;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentWorkflowInstances.Default)]
public abstract class DocumentWorkflowInstancesAppServiceBase : HCAppService
{
    protected IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDocumentWorkflowInstanceRepository _documentWorkflowInstanceRepository;
    protected DocumentWorkflowInstanceManager _documentWorkflowInstanceManager;
    protected IRepository<HC.Documents.Document, Guid> _documentRepository;
    protected IRepository<HC.Workflows.Workflow, Guid> _workflowRepository;
    protected IRepository<HC.WorkflowTemplates.WorkflowTemplate, Guid> _workflowTemplateRepository;
    protected IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;

    public DocumentWorkflowInstancesAppServiceBase(IDocumentWorkflowInstanceRepository documentWorkflowInstanceRepository, DocumentWorkflowInstanceManager documentWorkflowInstanceManager, IDistributedCache<DocumentWorkflowInstanceDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Documents.Document, Guid> documentRepository, IRepository<HC.Workflows.Workflow, Guid> workflowRepository, IRepository<HC.WorkflowTemplates.WorkflowTemplate, Guid> workflowTemplateRepository, IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> workflowStepTemplateRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _documentWorkflowInstanceRepository = documentWorkflowInstanceRepository;
        _documentWorkflowInstanceManager = documentWorkflowInstanceManager;
        _documentRepository = documentRepository;
        _workflowRepository = workflowRepository;
        _workflowTemplateRepository = workflowTemplateRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstancesInput input)
    {
        var totalCount = await _documentWorkflowInstanceRepository.GetCountAsync(input.FilterText, input.Status, input.StartedAtMin, input.StartedAtMax, input.FinishedAtMin, input.FinishedAtMax, input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId);
        var items = await _documentWorkflowInstanceRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Status, input.StartedAtMin, input.StartedAtMax, input.FinishedAtMin, input.FinishedAtMax, input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceWithNavigationProperties>, List<DocumentWorkflowInstanceWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentWorkflowInstanceWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstanceWithNavigationProperties, DocumentWorkflowInstanceWithNavigationPropertiesDto>(await _documentWorkflowInstanceRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentWorkflowInstanceDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(await _documentWorkflowInstanceRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Title != null && x.Title.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Documents.Document>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Documents.Document>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Workflows.Workflow>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Workflows.Workflow>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowTemplateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.WorkflowTemplates.WorkflowTemplate>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.WorkflowTemplates.WorkflowTemplate>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowStepTemplateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.WorkflowStepTemplates.WorkflowStepTemplate>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.WorkflowStepTemplates.WorkflowStepTemplate>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentWorkflowInstances.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentWorkflowInstanceRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstances.Create)]
    public virtual async Task<DocumentWorkflowInstanceDto> CreateAsync(DocumentWorkflowInstanceCreateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.WorkflowId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);
        }

        if (input.WorkflowTemplateId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowTemplate"]]);
        }

        if (input.CurrentStepId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowStepTemplate"]]);
        }

        var documentWorkflowInstance = await _documentWorkflowInstanceManager.CreateAsync(input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId, input.Status, input.StartedAt, input.FinishedAt);
        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(documentWorkflowInstance);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstances.Edit)]
    public virtual async Task<DocumentWorkflowInstanceDto> UpdateAsync(Guid id, DocumentWorkflowInstanceUpdateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.WorkflowId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Workflow"]]);
        }

        if (input.WorkflowTemplateId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowTemplate"]]);
        }

        if (input.CurrentStepId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["WorkflowStepTemplate"]]);
        }

        var documentWorkflowInstance = await _documentWorkflowInstanceManager.UpdateAsync(id, input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId, input.Status, input.StartedAt, input.FinishedAt, input.ConcurrencyStamp);
        return ObjectMapper.Map<DocumentWorkflowInstance, DocumentWorkflowInstanceDto>(documentWorkflowInstance);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentWorkflowInstanceExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var documentWorkflowInstances = await _documentWorkflowInstanceRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Status, input.StartedAtMin, input.StartedAtMax, input.FinishedAtMin, input.FinishedAtMax, input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId);
        var items = documentWorkflowInstances.Select(item => new { Status = item.DocumentWorkflowInstance.Status, StartedAt = item.DocumentWorkflowInstance.StartedAt, FinishedAt = item.DocumentWorkflowInstance.FinishedAt, Document = item.Document?.Title, Workflow = item.Workflow?.Name, WorkflowTemplate = item.WorkflowTemplate?.Name, CurrentStep = item.CurrentStep?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "DocumentWorkflowInstances.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.DocumentWorkflowInstances.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> documentworkflowinstanceIds)
    {
        await _documentWorkflowInstanceRepository.DeleteManyAsync(documentworkflowinstanceIds);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstances.Delete)]
    public virtual async Task DeleteAllAsync(GetDocumentWorkflowInstancesInput input)
    {
        await _documentWorkflowInstanceRepository.DeleteAllAsync(input.FilterText, input.Status, input.StartedAtMin, input.StartedAtMax, input.FinishedAtMin, input.FinishedAtMax, input.DocumentId, input.WorkflowId, input.WorkflowTemplateId, input.CurrentStepId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DocumentWorkflowInstanceDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}