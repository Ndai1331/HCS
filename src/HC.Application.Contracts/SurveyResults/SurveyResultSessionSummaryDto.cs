using System;

namespace HC.SurveyResults;

public class SurveyResultSessionSummaryDto
{
    public Guid SurveySessionId { get; set; }
    public double AverageRating { get; set; }
    public string? FullName { get; set; }
    public string? PhoneNumber { get; set; }
    public string? PatientCode { get; set; }
    public string? Note { get; set; }
    public DateTime SurveyTime { get; set; }
}
