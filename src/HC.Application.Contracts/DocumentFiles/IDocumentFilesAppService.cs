using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.DocumentFiles;

public partial interface IDocumentFilesAppService : IApplicationService
{
    Task<PagedResultDto<DocumentFileWithNavigationPropertiesDto>> GetListAsync(GetDocumentFilesInput input);
    Task<DocumentFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentFileDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentFileDto> CreateAsync(DocumentFileCreateDto input);
    Task<DocumentFileDto> UpdateAsync(Guid id, DocumentFileUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentFileExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> documentfileIds);
    Task DeleteAllAsync(GetDocumentFilesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}