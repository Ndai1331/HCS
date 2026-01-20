using Asp.Versioning;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Volo.Abp;
using Volo.Abp.AspNetCore.Mvc;
using Volo.Abp.Application.Dtos;
using HC.SurveyCriterias;

namespace HC.Controllers.SurveyCriterias;

[RemoteService]
[Area("app")]
[ControllerName("SurveyCriteria")]
[Route("api/app/survey-criterias")]
public class SurveyCriteriaController : SurveyCriteriaControllerBase, ISurveyCriteriasAppService
{
    public SurveyCriteriaController(ISurveyCriteriasAppService surveyCriteriasAppService) : base(surveyCriteriasAppService)
    {
    }

    [HttpGet]
    [Route("public/by-location/{surveyLocationId}")]
    public virtual Task<List<SurveyCriteriaDto>> GetPublicSurveyCriteriasByLocationAsync(Guid surveyLocationId)
    {
        return _surveyCriteriasAppService.GetPublicSurveyCriteriasByLocationAsync(surveyLocationId);
    }
}