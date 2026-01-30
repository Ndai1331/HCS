using System;

namespace HC.SurveyResults;

public class GetSurveyResultsInput : GetSurveyResultsInputBase
{
    public Guid? SurveyLocationId { get; set; }
}