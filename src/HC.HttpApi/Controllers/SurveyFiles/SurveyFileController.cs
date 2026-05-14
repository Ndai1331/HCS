using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyFiles;
using Volo.Abp.Content;

namespace HC.Controllers.SurveyFiles;

[RemoteService]
[Area("app")]
[ControllerName("SurveyFile")]
[Route("api/app/survey-files")]
public abstract class SurveyFileControllerBase : AbpController
{
    protected ISurveyFilesAppService _surveyFilesAppService;

    public SurveyFileControllerBase(ISurveyFilesAppService surveyFilesAppService)
    {
        _surveyFilesAppService = surveyFilesAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SurveyFileWithNavigationPropertiesDto>> GetListAsync(GetSurveyFilesInput input)
    {
        return _surveyFilesAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<SurveyFileWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _surveyFilesAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SurveyFileDto> GetAsync(Guid id)
    {
        return _surveyFilesAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("survey-session-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSurveySessionLookupAsync(LookupRequestDto input)
    {
        return _surveyFilesAppService.GetSurveySessionLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<SurveyFileDto> CreateAsync(SurveyFileCreateDto input)
    {
        return _surveyFilesAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SurveyFileDto> UpdateAsync(Guid id, SurveyFileUpdateDto input)
    {
        return _surveyFilesAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _surveyFilesAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyFileExcelDownloadDto input)
    {
        return _surveyFilesAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _surveyFilesAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> surveyfileIds)
    {
        return _surveyFilesAppService.DeleteByIdsAsync(surveyfileIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSurveyFilesInput input)
    {
        return _surveyFilesAppService.DeleteAllAsync(input);
    }
}