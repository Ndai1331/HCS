using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveySessions;
using Volo.Abp.Content;

namespace HC.Controllers.SurveySessions;

[RemoteService]
[Area("app")]
[ControllerName("SurveySession")]
[Route("api/app/survey-sessions")]
public abstract class SurveySessionControllerBase : AbpController
{
    protected ISurveySessionsAppService _surveySessionsAppService;

    public SurveySessionControllerBase(ISurveySessionsAppService surveySessionsAppService)
    {
        _surveySessionsAppService = surveySessionsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SurveySessionWithNavigationPropertiesDto>> GetListAsync(GetSurveySessionsInput input)
    {
        return _surveySessionsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<SurveySessionWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _surveySessionsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SurveySessionDto> GetAsync(Guid id)
    {
        return _surveySessionsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("survey-location-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input)
    {
        return _surveySessionsAppService.GetSurveyLocationLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<SurveySessionDto> CreateAsync(SurveySessionCreateDto input)
    {
        return _surveySessionsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SurveySessionDto> UpdateAsync(Guid id, SurveySessionUpdateDto input)
    {
        return _surveySessionsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _surveySessionsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveySessionExcelDownloadDto input)
    {
        return _surveySessionsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _surveySessionsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> surveysessionIds)
    {
        return _surveySessionsAppService.DeleteByIdsAsync(surveysessionIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSurveySessionsInput input)
    {
        return _surveySessionsAppService.DeleteAllAsync(input);
    }
}