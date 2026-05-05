using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Reports;

public partial interface IReportsAppService : IApplicationService
{
    Task<PagedResultDto<ReportDto>> GetListAsync(GetReportsInput input);
    Task<ReportDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<ReportDto> CreateAsync(ReportCreateDto input);
    Task<ReportDto> UpdateAsync(Guid id, ReportUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(ReportExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> reportIds);
    Task DeleteAllAsync(GetReportsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}