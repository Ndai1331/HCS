using HC.Chat.Helpers;
using HC.Shared;
using Volo.Abp.Identity;
using HC.Projects;
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
using HC.ProjectMembers;
using MiniExcelLibs;
using Volo.Abp.Content;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;
using Microsoft.Extensions.Caching.Distributed;

namespace HC.ProjectMembers;

[RemoteService(IsEnabled = false)]
[Authorize(HCPermissions.ProjectMembers.Default)]
public abstract class ProjectMembersAppServiceBase : HCAppService
{
    protected IDistributedCache<ProjectMemberDownloadTokenCacheItem, string> _downloadTokenCache;
    protected IProjectMemberRepository _projectMemberRepository;
    protected ProjectMemberManager _projectMemberManager;
    protected IRepository<HC.Projects.Project, Guid> _projectRepository;
    protected IRepository<Volo.Abp.Identity.IdentityUser, Guid> _identityUserRepository;

    public ProjectMembersAppServiceBase(IProjectMemberRepository projectMemberRepository, ProjectMemberManager projectMemberManager, IDistributedCache<ProjectMemberDownloadTokenCacheItem, string> downloadTokenCache, IRepository<HC.Projects.Project, Guid> projectRepository, IRepository<Volo.Abp.Identity.IdentityUser, Guid> identityUserRepository)
    {
        _downloadTokenCache = downloadTokenCache;
        _projectMemberRepository = projectMemberRepository;
        _projectMemberManager = projectMemberManager;
        _projectRepository = projectRepository;
        _identityUserRepository = identityUserRepository;
    }

    public virtual async Task<PagedResultDto<ProjectMemberWithNavigationPropertiesDto>> GetListAsync(GetProjectMembersInput input)
    {
        var totalCount = await _projectMemberRepository.GetCountAsync(input.FilterText, input.MemberRole, input.JoinedAtMin, input.JoinedAtMax, input.ProjectId, input.UserId);
        var items = await _projectMemberRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.MemberRole, input.JoinedAtMin, input.JoinedAtMax, input.ProjectId, input.UserId, input.Sorting, input.MaxResultCount, input.SkipCount);
        return new PagedResultDto<ProjectMemberWithNavigationPropertiesDto>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<ProjectMemberWithNavigationProperties>, List<ProjectMemberWithNavigationPropertiesDto>>(items)
        };
    }

    public virtual async Task<ProjectMemberWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectMemberWithNavigationProperties, ProjectMemberWithNavigationPropertiesDto>(await _projectMemberRepository.GetWithNavigationPropertiesAsync(id));
    }

    public virtual async Task<ProjectMemberDto> GetAsync(Guid id)
    {
        return ObjectMapper.Map<ProjectMember, ProjectMemberDto>(await _projectMemberRepository.GetAsync(id));
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input)
    {
        var query = (await _projectRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.Code != null && x.Code.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<HC.Projects.Project>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<HC.Projects.Project>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    public virtual async Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input)
    {
        var query = (await _identityUserRepository.GetQueryableAsync()).WhereIf(!string.IsNullOrWhiteSpace(input.Filter), x => (x.UserName != null && x.UserName.Contains(input.Filter)) || (x.Name != null && x.Name.Contains(input.Filter)));
        var lookupData = await query.PageBy(input.SkipCount, input.MaxResultCount).ToDynamicListAsync<Volo.Abp.Identity.IdentityUser>();
        var totalCount = query.Count();
        return new PagedResultDto<LookupDto<Guid>>
        {
            TotalCount = totalCount,
            Items = ObjectMapper.Map<List<Volo.Abp.Identity.IdentityUser>, List<LookupDto<Guid>>>(lookupData)
        };
    }

    protected virtual async Task EnsureCanManageProjectMembersAsync(Guid projectId)
    {
        var project = await _projectRepository.GetAsync(projectId);

        if (!CurrentUser.IsAdminRole() && (!CurrentUser.Id.HasValue || project.CreatorId != CurrentUser.Id.Value))
        {
            throw new UserFriendlyException("Only project creator can manage project members.");
        }
    }

    [Authorize(HCPermissions.ProjectMembers.Delete)]
    public virtual async Task DeleteAsync(Guid id)
    {
        var projectMember = await _projectMemberRepository.GetAsync(id);
        await EnsureCanManageProjectMembersAsync(projectMember.ProjectId);
        await _projectMemberRepository.DeleteAsync(id);
    }

    [Authorize(HCPermissions.ProjectMembers.Create)]
    public virtual async Task<ProjectMemberDto> CreateAsync(ProjectMemberCreateDto input)
    {
        if (input.ProjectId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Project"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        await EnsureCanManageProjectMembersAsync(input.ProjectId);

        // Store enum as string in database
        var memberRoleString = input.MemberRole.ToString();
        var projectMember = await _projectMemberManager.CreateAsync(input.ProjectId, input.UserId, memberRoleString, input.JoinedAt);
        return ObjectMapper.Map<ProjectMember, ProjectMemberDto>(projectMember);
    }

    [Authorize(HCPermissions.ProjectMembers.Edit)]
    public virtual async Task<ProjectMemberDto> UpdateAsync(Guid id, ProjectMemberUpdateDto input)
    {
        if (input.ProjectId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["Project"]]);
        }

        if (input.UserId == default)
        {
            throw new UserFriendlyException(L["The {0} field is required.", L["IdentityUser"]]);
        }

        await EnsureCanManageProjectMembersAsync(input.ProjectId);

        // Store enum as string in database
        var memberRoleString = input.MemberRole.ToString();
        var projectMember = await _projectMemberManager.UpdateAsync(id, input.ProjectId, input.UserId, memberRoleString, input.JoinedAt, input.ConcurrencyStamp);
        return ObjectMapper.Map<ProjectMember, ProjectMemberDto>(projectMember);
    }

    [AllowAnonymous]
    public virtual async Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectMemberExcelDownloadDto input)
    {
        await HC.ExcelDownloadAnonymousTokenHelper.ValidateAndConsumeOneTimeExportTokenAsync(_downloadTokenCache, input.DownloadToken, x => x.Token);

        var projectMembers = await _projectMemberRepository.GetListWithNavigationPropertiesAsync(input.FilterText, input.MemberRole, input.JoinedAtMin, input.JoinedAtMax, input.ProjectId, input.UserId);
        var items = projectMembers.Select(item => new { MemberRole = item.ProjectMember.MemberRole, JoinedAt = item.ProjectMember.JoinedAt, Project = item.Project?.Name, User = item.User?.Name, });
        var memoryStream = new MemoryStream();
        await memoryStream.SaveAsAsync(items);
        memoryStream.Seek(0, SeekOrigin.Begin);
        return new RemoteStreamContent(memoryStream, "ProjectMembers.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
    }

    [Authorize(HCPermissions.ProjectMembers.Delete)]
    public virtual async Task DeleteByIdsAsync(List<Guid> projectmemberIds)
    {
        await _projectMemberRepository.DeleteManyAsync(projectmemberIds);
    }

    [Authorize(HCPermissions.ProjectMembers.Delete)]
    public virtual async Task DeleteAllAsync(GetProjectMembersInput input)
    {
        await _projectMemberRepository.DeleteAllAsync(input.FilterText, input.MemberRole, input.JoinedAtMin, input.JoinedAtMax, input.ProjectId, input.UserId);
    }

    public virtual async Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        var token = Guid.NewGuid().ToString("N");
        await _downloadTokenCache.SetAsync(token, new ProjectMemberDownloadTokenCacheItem { Token = token }, new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(30) });
        return new HC.Shared.DownloadTokenResultDto
        {
            Token = token
        };
    }
}
