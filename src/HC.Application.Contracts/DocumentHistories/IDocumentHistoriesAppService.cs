using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.DocumentHistories;

public partial interface IDocumentHistoriesAppService : IApplicationService
{
    Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetListAsync(GetDocumentHistoriesInput input);
    Task<DocumentHistoryWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DocumentHistoryDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDocumentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DocumentHistoryDto> CreateAsync(DocumentHistoryCreateDto input);
    Task<DocumentHistoryDto> UpdateAsync(Guid id, DocumentHistoryUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DocumentHistoryExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> documenthistoryIds);
    Task DeleteAllAsync(GetDocumentHistoriesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
    Task<PagedResultDto<DocumentHistoryWithNavigationPropertiesDto>> GetHistoryByDocumentIdAsync(
        GetDocumentHistoriesInput input);
}