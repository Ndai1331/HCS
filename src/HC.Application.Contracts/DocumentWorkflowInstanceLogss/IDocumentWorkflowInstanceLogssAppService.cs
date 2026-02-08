using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HC.DocumentWorkflowInstanceLogss;

public partial interface IDocumentWorkflowInstanceLogssAppService : IApplicationService
{
    Task<PagedResultDto<DocumentWorkflowInstanceLogsDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input);
    Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceLogsListInput input);
    Task<PagedResultDto<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceLogssInput input);
    Task<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentWorkflowInstanceLogsDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentAssignmentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentWorkflowInstanceLogsDto> CreateAsync(DocumentWorkflowInstanceLogsCreateDto input);
    Task<DocumentWorkflowInstanceLogsDto> UpdateAsync(Guid id, DocumentWorkflowInstanceLogsUpdateDto input);
}