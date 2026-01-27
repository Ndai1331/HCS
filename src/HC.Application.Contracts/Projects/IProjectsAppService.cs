using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.Projects;

public partial interface IProjectsAppService : IApplicationService
{
    Task<PagedResultDto<ProjectWithNavigationPropertiesDto>> GetListAsync(GetProjectsInput input);
    Task<ProjectWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ProjectDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectDto> CreateAsync(ProjectCreateDto input);
    Task<ProjectDto> UpdateAsync(Guid id, ProjectUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> projectIds);
    Task DeleteAllAsync(GetProjectsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}