using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyLocations;
using Volo.Abp.Content;
using HC.Shared;

namespace HC.Controllers.SurveyLocations;

[RemoteService]
[Area("app")]
[ControllerName("SurveyLocation")]
[Route("api/app/survey-locations")]
public abstract class SurveyLocationControllerBase : AbpController
{
    protected ISurveyLocationsAppService _surveyLocationsAppService;

    public SurveyLocationControllerBase(ISurveyLocationsAppService surveyLocationsAppService)
    {
        _surveyLocationsAppService = surveyLocationsAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SurveyLocationDto>> GetListAsync(GetSurveyLocationsInput input)
    {
        return _surveyLocationsAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SurveyLocationDto> GetAsync(Guid id)
    {
        return _surveyLocationsAppService.GetAsync(id);
    }

    [HttpPost]
    public virtual Task<SurveyLocationDto> CreateAsync(SurveyLocationCreateDto input)
    {
        return _surveyLocationsAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SurveyLocationDto> UpdateAsync(Guid id, SurveyLocationUpdateDto input)
    {
        return _surveyLocationsAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _surveyLocationsAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyLocationExcelDownloadDto input)
    {
        return _surveyLocationsAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _surveyLocationsAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> surveylocationIds)
    {
        return _surveyLocationsAppService.DeleteByIdsAsync(surveylocationIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSurveyLocationsInput input)
    {
        return _surveyLocationsAppService.DeleteAllAsync(input);
    }

    [HttpGet]
    [Route("public/{id}")]
    public virtual async Task<SurveyLocationDto> GetPublicSurveyLocationAsync([FromRoute] Guid id)
    {
        return await _surveyLocationsAppService.GetPublicSurveyLocationAsync(id);
    }   
}