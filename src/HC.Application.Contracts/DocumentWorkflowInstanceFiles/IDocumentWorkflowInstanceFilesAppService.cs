using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;

namespace HC.DocumentWorkflowInstanceFiles;

public partial interface IDocumentWorkflowInstanceFilesAppService : IApplicationService
{
    Task<PagedResultDto<DocumentWorkflowInstanceFileDto>> GetListByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input);
    Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(GetDocumentWorkflowInstanceFileListInput input);
    Task<PagedResultDto<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentWorkflowInstanceFilesInput input);
    Task<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentWorkflowInstanceFileDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentFileLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentWorkflowInstanceFileDto> CreateAsync(DocumentWorkflowInstanceFileCreateDto input);
    Task<DocumentWorkflowInstanceFileDto> UpdateAsync(Guid id, DocumentWorkflowInstanceFileUpdateDto input);
}