using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.Documents;

public partial interface IDocumentsAppService : IApplicationService
{
    Task<PagedResultDto<DocumentWithNavigationPropertiesDto>> GetListAsync(GetDocumentsInput input);
    Task<DocumentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetMasterDataLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetUnitLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetWorkflowLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentDto> CreateAsync(DocumentCreateDto input);
    Task<DocumentDto> UpdateAsync(Guid id, DocumentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> documentIds);
    Task DeleteAllAsync(GetDocumentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
    Task<bool> SendDocumentAsync(SendDocumentInput input);
    Task<bool> RevokeDocumentAsync(RevokeDocumentInput input);
    Task<bool> SubmitForApprovalAsync(SubmitDocumentForApprovalInput input);
    Task<bool> RejectApprovalAsync(RejectDocumentApprovalInput input);
    Task<bool> ApproveWithNoteAsync(ApproveDocumentWithNoteInput input);
}
