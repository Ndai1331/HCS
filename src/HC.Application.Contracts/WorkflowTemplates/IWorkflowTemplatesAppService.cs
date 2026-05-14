using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.WorkflowTemplates;

public partial interface IWorkflowTemplatesAppService : IApplicationService
{
    Task<PagedResultDto<WorkflowTemplateWithNavigationPropertiesDto>> GetListAsync(GetWorkflowTemplatesInput input);
    Task<WorkflowTemplateWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<WorkflowTemplateDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<WorkflowTemplateDto> CreateAsync(WorkflowTemplateCreateDto input);
    Task<WorkflowTemplateDto> UpdateAsync(Guid id, WorkflowTemplateUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(WorkflowTemplateExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> workflowtemplateIds);
    Task DeleteAllAsync(GetWorkflowTemplatesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}