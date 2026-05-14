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

namespace HC.SurveySessions;

public abstract class EfCoreSurveySessionRepositoryBase : EfCoreRepository<HCDbContext, SurveySession, Guid>
{
    public EfCoreSurveySessionRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, fullName, phoneNumber, patientCode, surveyTimeMin, surveyTimeMax, deviceType, note, sessionDisplay, surveyLocationId);
        var ids = query.Select(x => x.SurveySession.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<SurveySessionWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        return await query
            .Where(x => x.SurveySession.Id == id)
            .FirstOrDefaultAsync(GetCancellationToken(cancellationToken));
    }

    public virtual async Task<List<SurveySessionWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null, Guid? surveyLocationId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, fullName, phoneNumber, patientCode, surveyTimeMin, surveyTimeMax, deviceType, note, sessionDisplay, surveyLocationId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveySessionConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<SurveySessionWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        return from surveySession in (await GetDbSetAsync())
               join surveyLocation in (await GetDbContextAsync()).Set<SurveyLocation>() on surveySession.SurveyLocationId equals surveyLocation.Id into surveyLocations
               from surveyLocation in surveyLocations.DefaultIfEmpty()
               select new SurveySessionWithNavigationProperties
               {
                   SurveySession = surveySession,
                   SurveyLocation = surveyLocation
               };
    }

    protected virtual IQueryable<SurveySessionWithNavigationProperties> ApplyFilter(IQueryable<SurveySessionWithNavigationProperties> query, string? filterText, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null, Guid? surveyLocationId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.SurveySession.FullName!.Contains(filterText!) || e.SurveySession.PhoneNumber!.Contains(filterText!) || e.SurveySession.PatientCode!.Contains(filterText!) || e.SurveySession.DeviceType!.Contains(filterText!) || e.SurveySession.Note!.Contains(filterText!) || e.SurveySession.SessionDisplay!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(fullName), e => e.SurveySession.FullName.Contains(fullName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.SurveySession.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(patientCode), e => e.SurveySession.PatientCode.Contains(patientCode)).WhereIf(surveyTimeMin.HasValue, e => e.SurveySession.SurveyTime >= surveyTimeMin!.Value).WhereIf(surveyTimeMax.HasValue, e => e.SurveySession.SurveyTime <= surveyTimeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(deviceType), e => e.SurveySession.DeviceType.Contains(deviceType)).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.SurveySession.Note.Contains(note)).WhereIf(!string.IsNullOrWhiteSpace(sessionDisplay), e => e.SurveySession.SessionDisplay.Contains(sessionDisplay)).WhereIf(surveyLocationId != null && surveyLocationId != Guid.Empty, e => e.SurveyLocation != null && e.SurveyLocation.Id == surveyLocationId);
    }

    public virtual async Task<List<SurveySession>> GetListAsync(string? filterText = null, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, fullName, phoneNumber, patientCode, surveyTimeMin, surveyTimeMax, deviceType, note, sessionDisplay);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? SurveySessionConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null, Guid? surveyLocationId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, fullName, phoneNumber, patientCode, surveyTimeMin, surveyTimeMax, deviceType, note, sessionDisplay, surveyLocationId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<SurveySession> ApplyFilter(IQueryable<SurveySession> query, string? filterText = null, string? fullName = null, string? phoneNumber = null, string? patientCode = null, DateTime? surveyTimeMin = null, DateTime? surveyTimeMax = null, string? deviceType = null, string? note = null, string? sessionDisplay = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.FullName!.Contains(filterText!) || e.PhoneNumber!.Contains(filterText!) || e.PatientCode!.Contains(filterText!) || e.DeviceType!.Contains(filterText!) || e.Note!.Contains(filterText!) || e.SessionDisplay!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(fullName), e => e.FullName.Contains(fullName)).WhereIf(!string.IsNullOrWhiteSpace(phoneNumber), e => e.PhoneNumber.Contains(phoneNumber)).WhereIf(!string.IsNullOrWhiteSpace(patientCode), e => e.PatientCode.Contains(patientCode)).WhereIf(surveyTimeMin.HasValue, e => e.SurveyTime >= surveyTimeMin!.Value).WhereIf(surveyTimeMax.HasValue, e => e.SurveyTime <= surveyTimeMax!.Value).WhereIf(!string.IsNullOrWhiteSpace(deviceType), e => e.DeviceType.Contains(deviceType)).WhereIf(!string.IsNullOrWhiteSpace(note), e => e.Note.Contains(note)).WhereIf(!string.IsNullOrWhiteSpace(sessionDisplay), e => e.SessionDisplay.Contains(sessionDisplay));
    }
}