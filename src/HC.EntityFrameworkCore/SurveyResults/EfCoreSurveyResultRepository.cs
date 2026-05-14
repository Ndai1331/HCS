using HC.SurveySessions;
using HC.SurveyCriterias;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Dynamic.Core;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Volo.Abp.Domain.Repositories.EntityFrameworkCore;
using Volo.Abp.EntityFrameworkCore;
using HC.EntityFrameworkCore;
using HC.SurveyLocations;

namespace HC.SurveyResults;

public abstract class EfCoreSurveyResultRepositoryBase : EfCoreRepository<HCDbContext, SurveyResult, Guid>
{
    public EfCoreSurveyResultRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, 
    Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, ratingMin, ratingMax, surveyCriteriaId, surveySessionId, surveyLocationId);
        var ids = query.Select(x => x.SurveyResult.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<SurveyResultWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.SurveyResult.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SurveyResultWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, ratingMin, ratingMax, surveyCriteriaId, surveySessionId, surveyLocationId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyResultConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<SurveyResultWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        var dbContext = await GetDbContextAsync();
        var surveyResults = await GetDbSetAsync();
        var surveyCriterias = dbContext.Set<SurveyCriteria>();
        var surveySessions = dbContext.Set<SurveySession>();
        var surveyLocations = dbContext.Set<SurveyLocation>();

        return from surveyResult in surveyResults
               join surveyCriteria in surveyCriterias on surveyResult.SurveyCriteriaId equals surveyCriteria.Id into surveyCriteriasJoin
               from surveyCriteria in surveyCriteriasJoin.DefaultIfEmpty()
               join surveySession in surveySessions on surveyResult.SurveySessionId equals surveySession.Id into surveySessionsJoin
               from surveySession in surveySessionsJoin.DefaultIfEmpty()
               join surveyLocation in surveyLocations on surveySession.SurveyLocationId equals surveyLocation.Id into surveyLocationsJoin
               from surveyLocation in surveyLocationsJoin.DefaultIfEmpty()
               select new SurveyResultWithNavigationProperties
               {
                   SurveyResult = surveyResult,
                   SurveyCriteria = surveyCriteria,
                   SurveySession = surveySession,
                   SurveyLocation = surveyLocation
               };
    }

    protected virtual IQueryable<SurveyResultWithNavigationProperties> ApplyFilter(IQueryable<SurveyResultWithNavigationProperties> query, string? filterText, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true)
        .WhereIf(ratingMin.HasValue, e => e.SurveyResult.Rating >= ratingMin!.Value)
        .WhereIf(ratingMax.HasValue, e => e.SurveyResult.Rating <= ratingMax!.Value)
        .WhereIf(surveyCriteriaId != null && surveyCriteriaId != Guid.Empty, e => e.SurveyCriteria != null && e.SurveyCriteria.Id == surveyCriteriaId)
        .WhereIf(surveySessionId != null && surveySessionId != Guid.Empty, e => e.SurveySession != null && e.SurveySession.Id == surveySessionId)
        .WhereIf(surveyLocationId != null && surveyLocationId != Guid.Empty, e => e.SurveyLocation != null && e.SurveyLocation.Id == surveyLocationId);
    }

    public virtual async Task<List<SurveyResult>> GetListAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, ratingMin, ratingMax, surveyCriteriaId, surveySessionId, surveyLocationId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyResultConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, ratingMin, ratingMax, surveyCriteriaId, surveySessionId, surveyLocationId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<SurveyResult> ApplyFilter(IQueryable<SurveyResult> query, string? filterText = null, int? ratingMin = null, int? ratingMax = null, Guid? surveyCriteriaId = null, Guid? surveySessionId = null, Guid? surveyLocationId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => true).WhereIf(ratingMin.HasValue, e => e.Rating >= ratingMin!.Value).WhereIf(ratingMax.HasValue, e => e.Rating <= ratingMax!.Value)
        .WhereIf(surveyCriteriaId != null && surveyCriteriaId != Guid.Empty, e => e.SurveyCriteriaId == surveyCriteriaId)
        .WhereIf(surveySessionId != null && surveySessionId != Guid.Empty, e => e.SurveySessionId == surveySessionId);
        // .WhereIf(surveyLocationId != null && surveyLocationId != Guid.Empty, e => e.SurveySession.SurveyLocationId == surveyLocationId);
    }
}