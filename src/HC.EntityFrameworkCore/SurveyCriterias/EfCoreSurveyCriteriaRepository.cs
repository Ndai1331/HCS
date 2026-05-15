using HC.SurveyLocations;
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

namespace HC.SurveyCriterias;

public abstract class EfCoreSurveyCriteriaRepositoryBase : EfCoreRepository<HCDbContext, SurveyCriteria, Guid>
{
    public EfCoreSurveyCriteriaRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, image, displayOrderMin, displayOrderMax, isActive, surveyLocationId);
        var ids = query.Select(x => x.SurveyCriteria.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<SurveyCriteriaWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.SurveyCriteria.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SurveyCriteriaWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, image, displayOrderMin, displayOrderMax, isActive, surveyLocationId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyCriteriaConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<SurveyCriteriaWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from surveyCriteria in (await GetDbSetAsync())
               join surveyLocation in (await GetDbContextAsync()).Set<SurveyLocation>() on surveyCriteria.SurveyLocationId equals surveyLocation.Id into surveyLocations
               from surveyLocation in surveyLocations.DefaultIfEmpty()
               select new SurveyCriteriaWithNavigationProperties
               {
                   SurveyCriteria = surveyCriteria,
                   SurveyLocation = surveyLocation
               };
    }

    protected virtual IQueryable<SurveyCriteriaWithNavigationProperties> ApplyFilter(IQueryable<SurveyCriteriaWithNavigationProperties> query, string? filterText, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null, Guid? surveyLocationId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.SurveyCriteria.Code!.Contains(filterText!) || e.SurveyCriteria.Name!.Contains(filterText!) || e.SurveyCriteria.Image!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.SurveyCriteria.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.SurveyCriteria.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(image), e => e.SurveyCriteria.Image.Contains(image)).WhereIf(displayOrderMin.HasValue, e => e.SurveyCriteria.DisplayOrder >= displayOrderMin!.Value).WhereIf(displayOrderMax.HasValue, e => e.SurveyCriteria.DisplayOrder <= displayOrderMax!.Value).WhereIf(isActive.HasValue, e => e.SurveyCriteria.IsActive == isActive).WhereIf(surveyLocationId != null && surveyLocationId != Guid.Empty, e => e.SurveyLocation != null && e.SurveyLocation.Id == surveyLocationId);
    }

    public virtual async Task<List<SurveyCriteria>> GetListAsync(string? filterText = null, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, code, name, image, displayOrderMin, displayOrderMax, isActive);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveyCriteriaConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, code, name, image, displayOrderMin, displayOrderMax, isActive, surveyLocationId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<SurveyCriteria> ApplyFilter(IQueryable<SurveyCriteria> query, string? filterText = null, string? code = null, string? name = null, string? image = null, int? displayOrderMin = null, int? displayOrderMax = null, bool? isActive = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.Code!.Contains(filterText!) || e.Name!.Contains(filterText!) || e.Image!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(code), e => e.Code.Contains(code)).WhereIf(!string.IsNullOrWhiteSpace(name), e => e.Name.Contains(name)).WhereIf(!string.IsNullOrWhiteSpace(image), e => e.Image.Contains(image)).WhereIf(displayOrderMin.HasValue, e => e.DisplayOrder >= displayOrderMin!.Value).WhereIf(displayOrderMax.HasValue, e => e.DisplayOrder <= displayOrderMax!.Value).WhereIf(isActive.HasValue, e => e.IsActive == isActive);
    }
}