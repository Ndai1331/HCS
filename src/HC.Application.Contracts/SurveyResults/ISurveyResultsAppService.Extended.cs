using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HC.SurveyResults;

public partial interface ISurveyResultsAppService
{
    // Public API - No authorization required
    Task<SurveyResultDto> CreatePublicSurveyResultAsync(SurveyResultCreateDto input);
    Task<List<SurveyResultDto>> CreatePublicSurveyResultsAsync(List<SurveyResultCreateDto> input);
    
    Task<SurveyResultStatisticsDto> GetStatisticsByLocationAsync(Guid? surveyLocationId);
}