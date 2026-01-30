using HC.SurveyCriterias;
using HC.SurveySessions;
using System;
using System.Collections.Generic;
using HC.SurveyResults;
using HC.SurveyLocations;

namespace HC.SurveyResults;

public abstract class SurveyResultWithNavigationPropertiesBase
{
    public SurveyResult SurveyResult { get; set; } = null!;
    public SurveyCriteria SurveyCriteria { get; set; } = null!;
    public SurveySession SurveySession { get; set; } = null!;
    public SurveyLocation SurveyLocation { get; set; } = null!;
}