using HC.Shared;
using Volo.Abp.Identity;
using HC.DocumentAssignments;
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
using HC.DocumentWorkflowInstanceLogss;

namespace HC.DocumentWorkflowInstanceLogss;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.DocumentWorkflowInstanceLogss.Default)]
public abstract class DocumentWorkflowInstanceLogssAppServiceBase : HCAppService
{
    protected IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;
    protected DocumentWorkflowInstanceLogsManager _documentWorkflowInstanceLogsManager;
    protected IRepository<HC.DocumentAssignments.DocumentAssignment, Guid> _documentAssignmentRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public DocumentWorkflowInstanceLogssAppServiceBase(IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository, DocumentWorkflowInstanceLogsManager documentWorkflowInstanceLogsManager, IRepository<HC.DocumentAssignments.DocumentAssignment, Guid> documentAssignmentRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
        _documentWorkflowInstanceLogsManager = documentWorkflowInstanceLogsManager;
        _documentAssignmentRepository = documentAssignmentRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceLogsDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input)
    {
        var documentWorkflowInstanceLogss = await _documentWorkflowInstanceLogsRepository.GetListByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceLogsDto>
        {
            TotalCount = await _documentWorkflowInstanceLogsRepository.GetCountByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId),
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceLogs>, List<DocumentWorkflowInstanceLogsDto>>(documentWorkflowInstanceLogss)
        };
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input)
    {
        var documentWorkflowInstanceLogss = await _documentWorkflowInstanceLogsRepository.GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>
        {
            TotalCount = await _documentWorkflowInstanceLogsRepository.GetCountByDocumentWorkflowInstanceIdAsync(input.DocumentWorkflowInstanceId),
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceLogsWithNavigationProperties>, List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>>(documentWorkflowInstanceLogss)
        };
    }

    public virtual async Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceLogssInput input)
    {
        var totalCount = await _documentWorkflowInstanceLogsRepository.GetCountAsync(input.FilterText, input.Action, input.ActorRole, input.FromStatus, input.ToStatus, input.Note, input.DocumentAssignmentId, input.ActorUserId);
        var items = await _documentWorkflowInstanceLogsRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.Action, input.ActorRole, input.FromStatus, input.ToStatus, input.Note, input.DocumentAssignmentId, input.ActorUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<DocumentWorkflowInstanceLogsWithNavigationProperties>, List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstanceLogsWithNavigationProperties, DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>(await _documentWorkflowInstanceLogsRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<DocumentWorkflowInstanceLogsDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<DocumentWorkflowInstanceLogs, DocumentWorkflowInstanceLogsDto>(await _documentWorkflowInstanceLogsRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetDocumentAssignmentLookupAsync(LookupRequestDto input)
    {
        var query = (await _documentAssignmentRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.ActionType != null && x.ActionType.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.DocumentAssignments.DocumentAssignment>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.DocumentAssignments.DocumentAssignment>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceLogss.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _documentWorkflowInstanceLogsRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceLogss.Create)]
    public virtual async Task<DocumentWorkflowInstanceLogsDto> CreateAsync(DocumentWorkflowInstanceLogsCreateDto input)
    {
        var documentWorkflowInstanceLogs = await _documentWorkflowInstanceLogsManager.CreateAsync(input.DocumentWorkflowInstanceId, input.DocumentAssignmentId, input.ActorUserId, input.Action, input.ActorRole, input.FromStatus, input.ToStatus, input.Note);
        return ObjectMapper.Map<DocumentWorkflowInstanceLogs, DocumentWorkflowInstanceLogsDto>(documentWorkflowInstanceLogs);
    }

    [Authorize(HCPermissions.DocumentWorkflowInstanceLogss.Edit)]
    public virtual async Task<DocumentWorkflowInstanceLogsDto> UpdateAsync(Guid id, DocumentWorkflowInstanceLogsUpdateDto input)
    {
        var documentWorkflowInstanceLogs = await _documentWorkflowInstanceLogsManager.UpdateAsync(id, input.DocumentWorkflowInstanceId, input.DocumentAssignmentId, input.ActorUserId, input.Action, input.ActorRole, input.FromStatus, input.ToStatus, input.Note);
        return ObjectMapper.Map<DocumentWorkflowInstanceLogs, DocumentWorkflowInstanceLogsDto>(documentWorkflowInstanceLogs);
    }
}