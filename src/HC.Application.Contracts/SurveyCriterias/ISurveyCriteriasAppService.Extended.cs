using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HC.SurveyCriterias;

public partial interface ISurveyCriteriasAppService
{
    // Public API - No authorization required
    Task<List<SurveyCriteriaDto>> GetPublicSurveyCriteriasByLocationAsync(Guid surveyLocationId);
}