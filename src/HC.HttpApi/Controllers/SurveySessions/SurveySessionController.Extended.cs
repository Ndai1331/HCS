using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveySessions;

namespace HC.Controllers.SurveySessions;

[RemoteService]
[Area("app")]
[ControllerName("SurveySession")]
[Route("api/app/survey-sessions")]
public class SurveySessionController : SurveySessionControllerBase, ISurveySessionsAppService
{
    public SurveySessionController(ISurveySessionsAppService surveySessionsAppService) : base(surveySessionsAppService)
    {
    }

    [HttpPost]
    [Route("public")]
    public virtual Task<SurveySessionDto> CreatePublicSurveySessionAsync(SurveySessionCreateDto input)
    {
        return _surveySessionsAppService.CreatePublicSurveySessionAsync(input);
    }
}