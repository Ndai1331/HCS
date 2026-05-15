using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.ProjectMembers;

public partial interface IProjectMembersAppService : IApplicationService
{
    Task<PagedResultDto<ProjectMemberWithNavigationPropertiesDto>> GetListAsync(GetProjectMembersInput input);
    Task<ProjectMemberWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ProjectMemberDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectMemberDto> CreateAsync(ProjectMemberCreateDto input);
    Task<ProjectMemberDto> UpdateAsync(Guid id, ProjectMemberUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectMemberExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> projectmemberIds);
    Task DeleteAllAsync(GetProjectMembersInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}