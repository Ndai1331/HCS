using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.WorkflowStepTemplates;

public partial interface IWorkflowStepTemplatesAppService : IApplicationService
{
    Task<PagedResultDto<WorkflowStepTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowStepTemplatesInput input);
    Task<WorkflowStepTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<WorkflowStepTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<WorkflowStepTemplateDto> CreateAsync(WorkflowStepTemplateCreateDto input);
    Task<WorkflowStepTemplateDto> UpdateAsync(Guid id, WorkflowStepTemplateUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowStepTemplateExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> workflowsteptemplateIds);
    Task DeleteAllAsync(GetWorkflowStepTemplatesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}