using System;
using System.Threading.Tasks;

namespace HC.SurveyFiles;

public partial interface ISurveyFilesAppService
{
    // Public API - No authorization required
    Task<SurveyFileDto> CreatePublicSurveyFileAsync(SurveyFileCreateDto input);
}