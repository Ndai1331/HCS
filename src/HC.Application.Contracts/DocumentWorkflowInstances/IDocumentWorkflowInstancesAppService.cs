using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.DocumentWorkflowInstances;

public partial interface IDocumentWorkflowInstancesAppService : IApplicationService
{
    Task<PagedResultDto<DocumentWorkflowInstanceWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstancesInput input);
    Task<DocumentWorkflowInstanceWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentWorkflowInstanceDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowTemplateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentWorkflowInstanceDto> CreateAsync(DocumentWorkflowInstanceCreateDto input);
    Task<DocumentWorkflowInstanceDto> UpdateAsync(Guid id, DocumentWorkflowInstanceUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentWorkflowInstanceExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> documentworkflowinstanceIds);
    Task DeleteAllAsync(GetDocumentWorkflowInstancesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}