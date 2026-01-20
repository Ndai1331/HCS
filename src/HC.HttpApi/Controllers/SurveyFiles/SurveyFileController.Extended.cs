using Asp.Versioning;
using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyFiles;

namespace HC.Controllers.SurveyFiles;

[RemoteService]
[Area("app")]
[ControllerName("SurveyFile")]
[Route("api/app/survey-files")]
public class SurveyFileController : SurveyFileControllerBase, ISurveyFilesAppService
{
    public SurveyFileController(ISurveyFilesAppService surveyFilesAppService) : base(surveyFilesAppService)
    {
    }

    [HttpPost]
    [Route("public")]
    public virtual Task<SurveyFileDto> CreatePublicSurveyFileAsync(SurveyFileCreateDto input)
    {
        return _surveyFilesAppService.CreatePublicSurveyFileAsync(input);
    }
}