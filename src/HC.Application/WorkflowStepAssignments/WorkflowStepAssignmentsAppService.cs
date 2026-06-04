using HC.Shared;
using Volo.Abp.Identity;
using HC.WorkflowStepTemplates;
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
using HC.WorkflowStepAssignments;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.WorkflowStepAssignments;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.WorkflowStepAssignments.Default)]
public abstract class WorkflowStepAssignmentsAppServiceBase : HCAppService
{
    protected IDistributedCache<WorkflowStepAssignmentDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IWorkflowStepAssignmentRepository _workflowStepAssignmentRepository;
    protected WorkflowStepAssignmentManager _workflowStepAssignmentManager;
    protected IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> _workflowStepTemplateRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;
    protected IRepository<IdentityRole, Guid> _identityRoleRepository;

    public WorkflowStepAssignmentsAppServiceBase(IWorkflowStepAssignmentRepository workflowStepAssignmentRepository, WorkflowStepAssignmentManager workflowStepAssignmentManager, IDistributedCache<WorkflowStepAssignmentDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.WorkflowStepTemplates.WorkflowStepTemplate, Guid> workflowStepTemplateRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository, IRepository<IdentityRole, Guid> identityRoleRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _workflowStepAssignmentRepository = workflowStepAssignmentRepository;
        _workflowStepAssignmentManager = workflowStepAssignmentManager;
        _workflowStepTemplateRepository = workflowStepTemplateRepository;
        _identityUserRepository = identityUserRepository;
        _identityRoleRepository = identityRoleRepository;
    }

    public virtual async Task<PagedResultDto<WorkflowStepAssignmentWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepAssignmentsInput input)
    {
        var totalCount = await _workflowStepAssignmentRepository.GetCountAsync(input.FilterText, input.IsPrimary, input.IsActive, input.StepId, input.DefaultUserId);
        var items = await _workflowStepAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsPrimary, input.IsActive, input.StepId, input.DefaultUserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        var dtos = new List<WorkflowStepAssignmentWithNavigationPropertiesDto>();
        foreach (var item in items)
        {
            var dto = ObjectMapper.Map<WorkflowStepAssignmentWithNavigationProperties, WorkflowStepAssignmentWithNavigationPropertiesDto>(item);
            ApplyScopeListsToDto(dto.WorkflowStepAssignment, item.WorkflowStepAssignment);
            dtos.Add(dto);
        }

        return new PagedResultDto<WorkflowStepAssignmentWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = dtos
        };
    }

    public virtual async Task<WorkflowStepAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        var item = await _workflowStepAssignmentRepository.GetWithNavigationPropertiesAsync(id);
        var dto = ObjectMapper.Map<WorkflowStepAssignmentWithNavigationProperties, WorkflowStepAssignmentWithNavigationPropertiesDto>(item);
        ApplyScopeListsToDto(dto.WorkflowStepAssignment, item.WorkflowStepAssignment);
        return dto;
    }

    protected virtual void ApplyScopeListsToDto(WorkflowStepAssignmentDto dto, WorkflowStepAssignment entity)
    {
        dto.OrganizationUnitIds = WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(entity.OrganizationUnitIdsJson);
        dto.DefaultUserIds = WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(entity.DefaultUserIdsJson, entity.DefaultUserId);
    }

    public virtual async Task<WorkflowStepAssignmentDto> GetAsync(Guid id)
    {
        return MapWorkflowStepAssignmentToDto(await _workflowStepAssignmentRepository.GetAsync(id));
    }

    protected virtual WorkflowStepAssignmentDto MapWorkflowStepAssignmentToDto(WorkflowStepAssignment entity)
    {
        var dto = ObjectMapper.Map<WorkflowStepAssignment, WorkflowStepAssignmentDto>(entity);
        dto.OrganizationUnitIds = WorkflowStepAssignmentScopeHelper.GetOrganizationUnitIds(entity.OrganizationUnitIdsJson);
        dto.DefaultUserIds = WorkflowStepAssignmentScopeHelper.GetDefaultUserIds(entity.DefaultUserIdsJson, entity.DefaultUserId);
        return dto;
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input)
    {
        var query = (await _workflowStepTemplateRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.WorkflowStepTemplates.WorkflowStepTemplate>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.WorkflowStepTemplates.WorkflowStepTemplate>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.UserName != null && x.UserName.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    [Authorize(HCPermissions.WorkflowStepAssignments.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        await _workflowStepAssignmentRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.WorkflowStepAssignments.Create)]
    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityRoleLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityRoleRepository.GetQueryableAsync())
            .WhereIf(!string.IsNullOrWhiteSpace(input.Filter),
                x => x.Name != null && x.Name.Contains(input.Filter));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<IdentityRole>();
        var totalCount = await AsyncExecuter.CountAsync(query);
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = lookupData.Select(x => new LookupDto<Guid>
            {
                Id = x.Id,
                DisplayName = x.Name ?? x.Id.ToString()
            }).ToList()
        };
    }

    public virtual async Task<WorkflowStepAssignmentDto> CreateAsync(WorkflowStepAssignmentCreateDto input)
    {
        var workflowStepAssignment = await _workflowStepAssignmentManager.CreateAsync(
            input.StepId,
            input.DefaultUserId,
            input.IsPrimary,
            input.IsActive,
            input.AssigneeType,
            input.RoleId,
            input.OrganizationUnitIds,
            input.DefaultUserIds);
        return MapWorkflowStepAssignmentToDto(workflowStepAssignment);
    }

    [Authorize(HCPermissions.WorkflowStepAssignments.Edit)]
    public virtual async Task<WorkflowStepAssignmentDto> UpdateAsync(Guid id, WorkflowStepAssignmentUpdateDto input)
    {
        var workflowStepAssignment = await _workflowStepAssignmentManager.UpdateAsync(
            id,
            input.StepId,
            input.DefaultUserId,
            input.IsPrimary,
            input.IsActive,
            input.AssigneeType,
            input.RoleId,
            input.OrganizationUnitIds,
            input.DefaultUserIds,
            input.ConcurrencyStamp);
        return MapWorkflowStepAssignmentToDto(workflowStepAssignment);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepAssignmentExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var workflowStepAssignments = await _workflowStepAssignmentRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.IsPrimary, input.IsActive, input.StepId, input.DefaultUserId);
        var items = workflowStepAssignments.Select(item => new { IsPrimary = item.WorkflowStepAssignment.IsPrimary, IsActive = item.WorkflowStepAssignment.IsActive, Step = item.Step?.Name, DefaultUser = item.DefaultUser?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "WorkflowStepAssignments.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.WorkflowStepAssignments.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> workflowstepassignmentIds)
    {
        await _workflowStepAssignmentRepository.DeleteManyAsync(workflowstepassignmentIds);
    }

    [Authorize(HCPermissions.WorkflowStepAssignments.Delete)]
    public virtual async Task DeleteAllAsync(GetWorkflowStepAssignmentsInput input)
    {
        await _workflowStepAssignmentRepository.DeleteAllAsync(input.FilterText, input.IsPrimary, input.IsActive, input.StepId, input.DefaultUserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new WorkflowStepAssignmentDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}