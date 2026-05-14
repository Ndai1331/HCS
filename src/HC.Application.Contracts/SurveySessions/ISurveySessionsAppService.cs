using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.SurveySessions;

public partial interface ISurveySessionsAppService : IApplicationService
{
    Task<PagedResultDto<SurveySessionWithNavigationPropertiesDto>> GetListAsync(GetSurveySessionsInput input);
    Task<SurveySessionWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<SurveySessionDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<SurveySessionDto> CreateAsync(SurveySessionCreateDto input);
    Task<SurveySessionDto> UpdateAsync(Guid id, SurveySessionUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveySessionExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> surveysessionIds);
    Task DeleteAllAsync(GetSurveySessionsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}