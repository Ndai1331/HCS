using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.UserDepartments;

public partial interface IUserDepartmentsAppService : IApplicationService
{
    Task<PagedResultDto<UserDepartmentWithNavigationPropertiesDto>> GetListAsync(GetUserDepartmentsInput input);
    Task<UserDepartmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<UserDepartmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetDepartmentLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<UserDepartmentDto> CreateAsync(UserDepartmentCreateDto input);
    Task<UserDepartmentDto> UpdateAsync(Guid id, UserDepartmentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(UserDepartmentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> userdepartmentIds);
    Task DeleteAllAsync(GetUserDepartmentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}