using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.ProjectTasks;

public partial interface IProjectTasksAppService : IApplicationService
{
    Task<PagedResultDto<ProjectTaskWithNavigationPropertiesDto>> GetListAsync(GetProjectTasksInput input);
    Task<ProjectTaskWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<ProjectTaskDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetProjectLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<ProjectTaskDto> CreateAsync(ProjectTaskCreateDto input);
    Task<ProjectTaskDto> UpdateAsync(Guid id, ProjectTaskUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ProjectTaskExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> projecttaskIds);
    Task DeleteAllAsync(GetProjectTasksInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}