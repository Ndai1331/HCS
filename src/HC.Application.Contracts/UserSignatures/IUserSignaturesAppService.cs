using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.UserSignatures;

public partial interface IUserSignaturesAppService : IApplicationService
{
    Task<PagedResultDto<UserSignatureWithNavigationPropertiesDto>> GetListAsync(GetUserSignaturesInput input);
    Task<UserSignatureWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<UserSignatureDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<UserSignatureDto> CreateAsync(UserSignatureCreateDto input);
    Task<UserSignatureDto> UpdateAsync(Guid id, UserSignatureUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserSignatureExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> usersignatureIds);
    Task DeleteAllAsync(GetUserSignaturesInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}