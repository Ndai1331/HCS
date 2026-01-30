using HC.Shared;
using Volo.Abp.Identity;
using HC.WorkflowStepTemplates;
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
using HC.DocumentAssignments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;
using HC.Shared;

namespace HC.DocumentAssignments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentAssignments.Default)]
public abstract class DocumentAssignmentsAppServiceBase : HCAppService
{
    protected IDistributedCache<DocumentAssignmentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IDocumentAssignmentRepository _documentAssignmentRepository;
    protected DocumentAssignmentManager _documentAssignmentManager;
    protected IRepository<HC.Documents.Document, Guid> _documentRepository;
    protected IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public DocumentAssignmentsAppServiceBase(IDocumentAssignmentRepository documentAssignmentRepository, DocumentAssignmentManager documentAssignmentManager, IDistributedCache<DocumentAssignmentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Documents.Document, Guid> documentRepository, IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> workflowStepTemplateRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _documentAssignmentRepository = documentAssignmentRepository;
        _documentAssignmentManager = documentAssignmentManager;
        _documentRepository = documentRepository;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<DocumentAssignmentWithNavigationPropertiesDto>> GetListAsync(GetDocumentAssignmentsInput input)
    {
        var totalCount = await _documentAssignmentRepository.GetCountAsync(input.FilterText, input.StepOrderMin, input.StepOrderMax, input.ActionType, input.Status, input.AssignedAtMin, input.AssignedAtMax, input.ProcessedAtMin, input.ProcessedAtMax, input.IsCurrent, input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId);
        var items = await _documentAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.StepOrderMin, input.StepOrderMax, input.ActionType, input.Status, input.AssignedAtMin, input.AssignedAtMax, input.ProcessedAtMin, input.ProcessedAtMax, input.IsCurrent, input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentAssignmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentAssignmentWithNavigationProperties>, List<DocumentAssignmentWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentAssignmentWithNavigationProperties, DocumentAssignmentWithNavigationPropertiesDto>(await _documentAssignmentRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentAssignmentDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentAssignment, DocumentAssignmentDto>(await _documentAssignmentRepository.GetAsync(id));
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

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentAssignments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentAssignmentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentAssignments.Create)]
    public virtual async Task<DocumentAssignmentDto> CreateAsync(DocumentAssignmentCreateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.ReceiverUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var documentAssignment = await _documentAssignmentManager.CreateAsync(input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId, input.StepOrder, input.ActionType.ToString(), input.Status.ToString(), input.AssignedAt, input.ProcessedAt, input.IsCurrent);
        return ObjectMapper.Map<DocumentAssignment, DocumentAssignmentDto>(documentAssignment);
    }

    [Authorize(HCPermissions.DocumentAssignments.Edit)]
    public virtual async Task<DocumentAssignmentDto> UpdateAsync(Guid id, DocumentAssignmentUpdateDto input)
    {
        if (input.DocumentId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Document"]]);
        }

        if (input.ReceiverUserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        var documentAssignment = await _documentAssignmentManager.UpdateAsync(id, input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId, input.StepOrder, input.ActionType, input.Status, input.AssignedAt, input.ProcessedAt, input.IsCurrent, input.ConcurrencyStamp);
        return ObjectMapper.Map<DocumentAssignment, DocumentAssignmentDto>(documentAssignment);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentAssignmentExcelDownloadDto input)
    {
        var downloadToken = await _downloadTokenCache.GetAsync(input.DownloadToken);
        if (downloadToken == null || input.DownloadToken != downloadToken.Token)
        {
            throw new AbpAuthorizationException("Invalid download token: " + input.DownloadToken);
        }

        var documentAssignments = await _documentAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.StepOrderMin, input.StepOrderMax, input.ActionType, input.Status, input.AssignedAtMin, input.AssignedAtMax, input.ProcessedAtMin, input.ProcessedAtMax, input.IsCurrent, input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId);
        var items = documentAssignments.Select(item => new { StepOrder = item.DocumentAssignment.StepOrder, ActionType = item.DocumentAssignment.ActionType, Status = item.DocumentAssignment.Status, AssignedAt = item.DocumentAssignment.AssignedAt, ProcessedAt = item.DocumentAssignment.ProcessedAt, IsCurrent = item.DocumentAssignment.IsCurrent, Document = item.Document?.Title, WorkflowStepTemplate = item.WorkflowStepTemplate?.Name, ReceiverUser = item.ReceiverUser?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "DocumentAssignments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.DocumentAssignments.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> documentassignmentIds)
    {
        await _documentAssignmentRepository.DeleteManyAsync(documentassignmentIds);
    }

    [Authorize(HCPermissions.DocumentAssignments.Delete)]
    public virtual async Task DeleteAllAsync(GetDocumentAssignmentsInput input)
    {
        await _documentAssignmentRepository.DeleteAllAsync(input.FilterText, input.StepOrderMin, input.StepOrderMax, input.ActionType, input.Status, input.AssignedAtMin, input.AssignedAtMax, input.ProcessedAtMin, input.ProcessedAtMax, input.IsCurrent, input.DocumentId, input.WorkflowStepTemplateId, input.ReceiverUserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new DocumentAssignmentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}