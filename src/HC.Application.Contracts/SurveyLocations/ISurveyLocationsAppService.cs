using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.SurveyLocations;

public partial interface ISurveyLocationsAppService : IApplicationService
{
    Task<PagedResultDto<SurveyLocationDto>> GetListAsync(GetSurveyLocationsInput input);
    Task<SurveyLocationDto> GetAsync(Guid id);
    Task DeleteAsync(Guid id);
    Task<SurveyLocationDto> CreateAsync(SurveyLocationCreateDto input);
    Task<SurveyLocationDto> UpdateAsync(Guid id, SurveyLocationUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyLocationExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> surveylocationIds);
    Task DeleteAllAsync(GetSurveyLocationsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();

    Task<SurveyLocationDto> GetPublicSurveyLocationAsync(Guid id);
}