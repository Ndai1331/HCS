using System;
using System.Threading.Tasks;

namespace HC.SurveySessions;

public partial interface ISurveySessionsAppService
{
    // Public API - No authorization required
    Task<SurveySessionDto> CreatePublicSurveySessionAsync(SurveySessionCreateDto input);
}