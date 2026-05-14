using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyResults;
using Volo.Abp.Content;

namespace HC.Controllers.SurveyResults;

[RemoteService]
[Area("app")]
[ControllerName("SurveyResult")]
[Route("api/app/survey-results")]
public abstract class SurveyResultControllerBase : AbpController
{
    protected ISurveyResultsAppService _surveyResultsAppService;

    public SurveyResultControllerBase(ISurveyResultsAppService surveyResultsAppService)
    {
        _surveyResultsAppService = surveyResultsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SurveyResultWithNavigationPropertiesDto>> GetListAsync(GetSurveyResultsInput input)
    {
        return _surveyResultsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<SurveyResultWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _surveyResultsAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SurveyResultDto> GetAsync(Guid id)
    {
        return _surveyResultsAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("survey-criteria-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSurveyCriteriaLookupAsync(LookupRequestDto input)
    {
        return _surveyResultsAppService.GetSurveyCriteriaLookupAsync(input);
    }

    [HttpGet]
    [Route("survey-session-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input)
    {
        return _surveyResultsAppService.GetSurveySessionLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<SurveyResultDto> CreateAsync(SurveyResultCreateDto input)
    {
        return _surveyResultsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SurveyResultDto> UpdateAsync(Guid id, SurveyResultUpdateDto input)
    {
        return _surveyResultsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _surveyResultsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyResultExcelDownloadDto input)
    {
        return _surveyResultsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _surveyResultsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> surveyresultIds)
    {
        return _surveyResultsAppService.DeleteByIdsAsync(surveyresultIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSurveyResultsInput input)
    {
        return _surveyResultsAppService.DeleteAllAsync(input);
    }
}