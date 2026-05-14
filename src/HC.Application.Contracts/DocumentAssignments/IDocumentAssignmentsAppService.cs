using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.DocumentAssignments;

public partial interface IDocumentAssignmentsAppService : IApplicationService
{
    Task<PagedResultDto<DocumentAssignmentWithNavigationPropertiesDto>> GetListAsync(GetDocumentAssignmentsInput input);
    Task<DocumentAssignmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentAssignmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowStepTemplateLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentAssignmentDto> CreateAsync(DocumentAssignmentCreateDto input);
    Task<DocumentAssignmentDto> UpdateAsync(Guid id, DocumentAssignmentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentAssignmentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> documentassignmentIds);
    Task DeleteAllAsync(GetDocumentAssignmentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}