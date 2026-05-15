using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.Departments;

public partial interface IDepartmentsAppService : IApplicationService
{
    Task<PagedResultDto<DepartmentWithNavigationPropertiesDto>> GetListAsync(GetDepartmentsInput input);
    Task<DepartmentWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<DepartmentDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetIdentityUserLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<DepartmentDto> CreateAsync(DepartmentCreateDto input);
    Task<DepartmentDto> UpdateAsync(Guid id, DepartmentUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(DepartmentExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> departmentIds);
    Task DeleteAllAsync(GetDepartmentsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}