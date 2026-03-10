using System;

namespace HC.SurveyResults;

public class GetSurveyResultSessionDetailsInput
{
    public Guid SurveySessionId { get; set; }
    public Guid? SurveyLocationId { get; set; }
}
