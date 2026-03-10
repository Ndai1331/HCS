using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Volo.Abp.Application.Dtos;

namespace HC.SurveyResults;

public partial interface ISurveyResultsAppService
{
    // Public API - No authorization required
    Task<SurveyResultDto> CreatePublicSurveyResultAsync(SurveyResultCreateDto input);
    Task<List<SurveyResultDto>> CreatePublicSurveyResultsAsync(List<SurveyResultCreateDto> input);
    
    Task<SurveyResultStatisticsDto> GetStatisticsByLocationAsync(Guid? surveyLocationId);
    Task<PagedResultDto<SurveyResultSessionSummaryDto>> GetSessionSummaryListAsync(GetSurveyResultSessionSummariesInput input);
    Task<List<SurveyResultSessionDetailDto>> GetSessionDetailListAsync(GetSurveyResultSessionDetailsInput input);
}