using System;
using System.Threading.Tasks;

namespace HC.SurveyResults;

public partial interface ISurveyResultsAppService
{
    // Public API - No authorization required
    Task<SurveyResultDto> CreatePublicSurveyResultAsync(SurveyResultCreateDto input);
    
    // Statistics API
    Task<SurveyResultStatisticsDto> GetStatisticsByLocationAsync(Guid? surveyLocationId);
}