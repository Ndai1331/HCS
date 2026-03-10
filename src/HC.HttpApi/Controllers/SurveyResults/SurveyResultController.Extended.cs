using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyResults;
using System.Collections.Generic;

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

    [HttpPost]
    [Route("public/bulk")]
    public virtual Task<List<SurveyResultDto>> CreatePublicSurveyResultsAsync(List<SurveyResultCreateDto> input)
    {
        return _surveyResultsAppService.CreatePublicSurveyResultsAsync(input);
    }

    [HttpGet]
    [Route("public/statistics")]
    public virtual Task<SurveyResultStatisticsDto> GetStatisticsByLocationAsync(Guid? surveyLocationId)
    {
        return _surveyResultsAppService.GetStatisticsByLocationAsync(surveyLocationId);
    }

    [HttpGet]
    [Route("session-summaries")]
    public virtual Task<PagedResultDto<SurveyResultSessionSummaryDto>> GetSessionSummaryListAsync([FromQuery] GetSurveyResultSessionSummariesInput input)
    {
        return _surveyResultsAppService.GetSessionSummaryListAsync(input);
    }

    [HttpGet]
    [Route("session-details")]
    public virtual Task<List<SurveyResultSessionDetailDto>> GetSessionDetailListAsync([FromQuery] GetSurveyResultSessionDetailsInput input)
    {
        return _surveyResultsAppService.GetSessionDetailListAsync(input);
    }
}