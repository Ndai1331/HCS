using System;
using System.Collections.Generic;

namespace HC.SurveyResults;

public class SurveyResultStatisticsDto
{
    /// <summary>
    /// Total number of review rows by selected location
    /// </summary>
    public int TotalReviews { get; set; }

    /// <summary>
    /// Rating distribution: Key = Rating (0-5), Value = Count
    /// </summary>
    public Dictionary<int, int> RatingDistribution { get; set; } = new();

    /// <summary>
    /// Criteria average ratings: Key = Criteria Name, Value = Average Rating
    /// </summary>
    public Dictionary<string, double> CriteriaAverageRatings { get; set; } = new();
}
