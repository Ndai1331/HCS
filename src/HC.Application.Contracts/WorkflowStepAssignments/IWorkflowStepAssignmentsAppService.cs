using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.WorkflowStepAssignments;

public partial interface IWorkflowStepAssignmentsAppService : IApplicationService
{
    Task<PagedResultDto<WorkflowStepAssignmentWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepAssignmentsInput input);
    Task<WorkflowStepAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<WorkflowStepAssignmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<WorkflowStepAssignmentDto> CreateAsync(WorkflowStepAssignmentCreateDto input);
    Task<WorkflowStepAssignmentDto> UpdateAsync(Guid id, WorkflowStepAssignmentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepAssignmentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> workflowstepassignmentIds);
    Task DeleteAllAsync(GetWorkflowStepAssignmentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}