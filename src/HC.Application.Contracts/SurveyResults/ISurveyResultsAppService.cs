using HC.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Application.Services;
using Volo.Abp.Content;

namespace HC.SurveyResults;

public partial interface ISurveyResultsAppService : IApplicationService
{
    Task<PagedResultDto<SurveyResultWithNavigationPropertiesDto>> GetListAsync(GetSurveyResultsInput input);
    Task<SurveyResultWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id);
    Task<SurveyResultDto> GetAsync(Guid id);
    Task<PagedResultDto<LookupDto<Guid>>> GetSurveyCriteriaLookupAsync(LookupRequestDto input);
    Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input);
    Task DeleteAsync(Guid id);
    Task<SurveyResultDto> CreateAsync(SurveyResultCreateDto input);
    Task<SurveyResultDto> UpdateAsync(Guid id, SurveyResultUpdateDto input);
    Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyResultExcelDownloadDto input);
    Task DeleteByIdsAsync(List<Guid> surveyresultIds);
    Task DeleteAllAsync(GetSurveyResultsInput input);
    Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync();
}