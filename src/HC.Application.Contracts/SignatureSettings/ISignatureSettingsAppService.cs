using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.SignatureSettings;

public partial interface ISignatureSettingsAppService : IApplicationService
{
    Task<PagedResultDto<SignatureSettingDto>> GetListAsync(GetSignatureSettingsInput input);
    Task<SignatureSettingDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<SignatureSettingDto> CreateAsync(SignatureSettingCreateDto input);
    Task<SignatureSettingDto> UpdateAsync(Guid id, SignatureSettingUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SignatureSettingExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> signaturesettingIds);
    Task DeleteAllAsync(GetSignatureSettingsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
    Task<PagedResultDto<LookupDto<Guid>>> GetSignatureSettingLookupAsync(LookupRequestDto input);
}