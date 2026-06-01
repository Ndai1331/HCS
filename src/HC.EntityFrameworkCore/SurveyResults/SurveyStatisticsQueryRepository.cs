using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.SurveyCriterias;
using HC.SurveySessions;
using Volo.Abp;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Linq;

namespace HC.SurveyResults;

public class SurveyStatisticsQueryRepository : ISurveyStatisticsQueryRepository, ITransientDependency
{
    private readonly ISurveyResultRepository _surveyResultRepository;
    private readonly IRepository<SurveySession, Guid> _surveySessionRepository;
    private readonly IRepository<SurveyCriteria, Guid> _surveyCriteriaRepository;
    private readonly IAsyncQueryableExecuter _asyncExecuter;
    private readonly IDataFilter _dataFilter;

    public SurveyStatisticsQueryRepository(
        ISurveyResultRepository surveyResultRepository,
        IRepository<SurveySession, Guid> surveySessionRepository,
        IRepository<SurveyCriteria, Guid> surveyCriteriaRepository,
        IAsyncQueryableExecuter asyncExecuter,
        IDataFilter dataFilter)
    {
        _surveyResultRepository = surveyResultRepository;
        _surveySessionRepository = surveySessionRepository;
        _surveyCriteriaRepository = surveyCriteriaRepository;
        _asyncExecuter = asyncExecuter;
        _dataFilter = dataFilter;
    }

    public async Task<SurveyStatisticsQueryResult> GetStatisticsByLocationAsync(
        Guid? surveyLocationId,
        CancellationToken cancellationToken = default)
    {
        using (_dataFilter.Disable<ISoftDelete>())
        {
            var queryable = await _surveyResultRepository.GetQueryableAsync();
            var sessionQueryable = await _surveySessionRepository.GetQueryableAsync();
            var criteriaQueryable = await _surveyCriteriaRepository.GetQueryableAsync();

            var query = from surveyResult in queryable
                        join session in sessionQueryable on surveyResult.SurveySessionId equals session.Id
                        join criteria in criteriaQueryable on surveyResult.SurveyCriteriaId equals criteria.Id
                        where !surveyLocationId.HasValue || session.SurveyLocationId == surveyLocationId.Value
                        select new
                        {
                            surveyResult.Rating,
                            criteria.Id,
                            criteria.Name,
                            criteria.Code
                        };

            var data = await _asyncExecuter.ToListAsync(query, cancellationToken);

            var statistics = new SurveyStatisticsQueryResult
            {
                TotalReviews = data.Count,
                RatingDistribution = data
                    .GroupBy(x => x.Rating)
                    .ToDictionary(g => g.Key, g => g.Count())
            };

            for (var i = 0; i <= 5; i++)
            {
                if (!statistics.RatingDistribution.ContainsKey(i))
                {
                    statistics.RatingDistribution[i] = 0;
                }
            }

            var criteriaAverages = data
                .GroupBy(x => new { x.Id, x.Name, x.Code })
                .Select(g => new
                {
                    CriteriaName = g.Key.Name,
                    CriteriaCode = g.Key.Code,
                    AverageRating = g.Average(x => x.Rating)
                })
                .OrderBy(x => x.CriteriaName)
                .ToList();

            statistics.CriteriaAverageRatings = criteriaAverages
                .GroupBy(x => x.CriteriaName)
                .SelectMany(group => group.Select(item => new
                {
                    Label = group.Count() > 1 && !string.IsNullOrWhiteSpace(item.CriteriaCode)
                        ? $"{item.CriteriaName} ({item.CriteriaCode})"
                        : item.CriteriaName,
                    item.AverageRating
                }))
                .ToDictionary(x => x.Label, x => Math.Round(x.AverageRating, 1));

            return statistics;
        }
    }
}
