using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.SurveyResults;

public partial interface ISurveyResultRepository : IRepository<SurveyResult, Guid>
{
    Task DeleteAllAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default);
    Task<SurveyResultWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<SurveyResultWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<SurveyResult>> GetListAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default);
}