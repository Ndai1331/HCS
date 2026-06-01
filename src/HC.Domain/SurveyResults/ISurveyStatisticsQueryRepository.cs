using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace HC.SurveyResults;

public interface ISurveyStatisticsQueryRepository
{
    Task<SurveyStatisticsQueryResult> GetStatisticsByLocationAsync(
        Guid? surveyLocationId,
        CancellationToken cancellationToken = default);
}

public class SurveyStatisticsQueryResult
{
    public int TotalReviews { get; set; }

    public Dictionary<int, int> RatingDistribution { get; set; } = new();

    public Dictionary<string, double> CriteriaAverageRatings { get; set; } = new();
}
