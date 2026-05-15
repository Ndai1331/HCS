using HC.Shared;
using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyCriterias;
using Volo.Abp.Content;

namespace HC.Controllers.SurveyCriterias;

[RemoteService]
[Area("app")]
[ControllerName("SurveyCriteria")]
[Route("api/app/survey-criterias")]
public abstract class SurveyCriteriaControllerBase : AbpController
{
    protected ISurveyCriteriasAppService _surveyCriteriasAppService;

    public SurveyCriteriaControllerBase(ISurveyCriteriasAppService surveyCriteriasAppService)
    {
        _surveyCriteriasAppService = surveyCriteriasAppService;
    }

    [HttpGet]
    public virtual Task<PagedResultDto<SurveyCriteriaWithNavigationPropertiesDto>> GetListAsync(GetSurveyCriteriasInput input)
    {
        return _surveyCriteriasAppService.GetListAsync(input);
    }

    [HttpGet]
    [Route("with-navigation-properties/{id}")]
    public virtual Task<SurveyCriteriaWithNavigationPropertiesDto> GetWithNavigationPropertiesAsync(Guid id)
    {
        return _surveyCriteriasAppService.GetWithNavigationPropertiesAsync(id);
    }

    [HttpGet]
    [Route("{id}")]
    public virtual Task<SurveyCriteriaDto> GetAsync(Guid id)
    {
        return _surveyCriteriasAppService.GetAsync(id);
    }

    [HttpGet]
    [Route("survey-location-lookup")]
    public virtual Task<PagedResultDto<LookupDto<Guid>>> GetSurveyLocationLookupAsync(LookupRequestDto input)
    {
        return _surveyCriteriasAppService.GetSurveyLocationLookupAsync(input);
    }

    [HttpPost]
    public virtual Task<SurveyCriteriaDto> CreateAsync(SurveyCriteriaCreateDto input)
    {
        return _surveyCriteriasAppService.CreateAsync(input);
    }

    [HttpPut]
    [Route("{id}")]
    public virtual Task<SurveyCriteriaDto> UpdateAsync(Guid id, SurveyCriteriaUpdateDto input)
    {
        return _surveyCriteriasAppService.UpdateAsync(id, input);
    }

    [HttpDelete]
    [Route("{id}")]
    public virtual Task DeleteAsync(Guid id)
    {
        return _surveyCriteriasAppService.DeleteAsync(id);
    }

    [HttpGet]
    [Route("as-excel-file")]
    public virtual Task<IRemoteStreamContent> GetListAsExcelFileAsync(SurveyCriteriaExcelDownloadDto input)
    {
        return _surveyCriteriasAppService.GetListAsExcelFileAsync(input);
    }

    [HttpGet]
    [Route("download-token")]
    public virtual Task<HC.Shared.DownloadTokenResultDto> GetDownloadTokenAsync()
    {
        return _surveyCriteriasAppService.GetDownloadTokenAsync();
    }

    [HttpDelete]
    [Route("")]
    public virtual Task DeleteByIdsAsync(List<Guid> surveycriteriaIds)
    {
        return _surveyCriteriasAppService.DeleteByIdsAsync(surveycriteriaIds);
    }

    [HttpDelete]
    [Route("all")]
    public virtual Task DeleteAllAsync(GetSurveyCriteriasInput input)
    {
        return _surveyCriteriasAppService.DeleteAllAsync(input);
    }
}