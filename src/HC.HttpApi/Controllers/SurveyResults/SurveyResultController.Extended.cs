using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyResults;

namespace HC.Controllers.SurveyResults;

[RemoteService]
[Area("app")]
[ControllerName("SurveyResult")]
[Route("api/app/survey-results")]
public class SurveyResultController : SurveyResultControllerBase, ISurveyResultsAppService
{
    public SurveyResultController(ISurveyResultsAppService surveyResultsAppService) : base(surveyResultsAppService)
    {
    }

    [HttpPost]
    [Route("public")]
    public virtual Task<SurveyResultDto> CreatePublicSurveyResultAsync(SurveyResultCreateDto input)
    {
        return _surveyResultsAppService.CreatePublicSurveyResultAsync(input);
    }
}