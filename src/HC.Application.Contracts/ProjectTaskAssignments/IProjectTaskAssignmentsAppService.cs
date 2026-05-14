using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.ProjectTaskAssignments;

public partial interface IProjectTaskAssignmentsAppService : IApplicationService
{
    Task<PagedResultDto<ProjectTaskAssignmentWithNavigationPropertiesDto>> GetListAsync(GetProjectTaskAssignmentsInput input);
    Task<ProjectTaskAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ProjectTaskAssignmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetProjectTaskLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectTaskAssignmentDto> CreateAsync(ProjectTaskAssignmentCreateDto input);
    Task<ProjectTaskAssignmentDto> UpdateAsync(Guid id, ProjectTaskAssignmentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskAssignmentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> projecttaskassignmentIds);
    Task DeleteAllAsync(GetProjectTaskAssignmentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}