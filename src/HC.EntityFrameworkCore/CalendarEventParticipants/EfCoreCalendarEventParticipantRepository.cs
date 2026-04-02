using Volo.Abp.Identity;
using HC.CalendarEvents;
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

namespace HC.CalendarEventParticipants;

public abstract class EfCoreCalendarEventParticipantRepositoryBase : EfCoreRepository<HCDbContext, CalendarEventParticipant, Guid>
{
    public EfCoreCalendarEventParticipantRepositoryBase(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task DeleteAllAsync(string? filterText = null, string? responseStatus = null, bool? notified = null, Guid? calendarEventId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, responseStatus, notified, calendarEventId, identityUserId);
        var ids = query.Select(x => x.CalendarEventParticipant.Id);
        await DeleteManyAsync(ids, cancellationToken: GetCancellationToken(cancellationToken));
    }

    public virtual async Task<CalendarEventParticipantWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dbContext = await GetDbContextAsync();
        return (await GetDbSetAsync()).Where(b => b.Id == id).Select(calendarEventParticipant => new CalendarEventParticipantWithNavigationProperties { CalendarEventParticipant = calendarEventParticipant, CalendarEvent = dbContext.Set<CalendarEvent>().FirstOrDefault(c => c.Id == calendarEventParticipant.CalendarEventId), IdentityUser = dbContext.Set<IdentityUser>().FirstOrDefault(c => c.Id == calendarEventParticipant.IdentityUserId) }).FirstOrDefault();
    }

    public virtual async Task<List<CalendarEventParticipantWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? responseStatus = null, bool? notified = null, Guid? calendarEventId = null, Guid? identityUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, responseStatus, notified, calendarEventId, identityUserId);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? CalendarEventParticipantConsts.GetDefaultSorting(true) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    protected virtual async Task<IQueryable<CalendarEventParticipantWithNavigationProperties>> GetQueryForNavigationPropertiesAsync()
    {
        var dbContext = await GetDbContextAsync();
        var calendarEventsSet = dbContext.Set<CalendarEvent>();
        var identityUsersSet = dbContext.Set<IdentityUser>();
        return from calendarEventParticipant in (await GetDbSetAsync())
               join calendarEvent in calendarEventsSet on calendarEventParticipant.CalendarEventId equals calendarEvent.Id into calendarEvents
               from calendarEvent in calendarEvents.DefaultIfEmpty()
               join identityUser in identityUsersSet on calendarEventParticipant.IdentityUserId equals identityUser.Id into identityUsers
               from identityUser in identityUsers.DefaultIfEmpty()
               select new CalendarEventParticipantWithNavigationProperties
               {
                   CalendarEventParticipant = calendarEventParticipant,
                   CalendarEvent = calendarEvent,
                   IdentityUser = identityUser
               };
    }

    protected virtual IQueryable<CalendarEventParticipantWithNavigationProperties> ApplyFilter(IQueryable<CalendarEventParticipantWithNavigationProperties> query, string? filterText, string? responseStatus = null, bool? notified = null, Guid? calendarEventId = null, Guid? identityUserId = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.CalendarEventParticipant.ResponseStatus!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(responseStatus), e => e.CalendarEventParticipant.ResponseStatus.Contains(responseStatus)).WhereIf(notified.HasValue, e => e.CalendarEventParticipant.Notified == notified).WhereIf(calendarEventId != null && calendarEventId != Guid.Empty, e => e.CalendarEvent != null && e.CalendarEvent.Id == calendarEventId).WhereIf(identityUserId != null && identityUserId != Guid.Empty, e => e.IdentityUser != null && e.IdentityUser.Id == identityUserId);
    }

    public virtual async Task<List<CalendarEventParticipant>> GetListAsync(string? filterText = null, string? responseStatus = null, bool? notified = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default)
    {
        var query = ApplyFilter((await GetQueryableAsync()), filterText, responseStatus, notified);
        query = query.OrderBy(string.IsNullOrWhiteSpace(sorting) ? CalendarEventParticipantConsts.GetDefaultSorting(false) : sorting);
        return await query.PageBy(skipCount, maxResultCount).ToListAsync(cancellationToken);
    }

    public virtual async Task<long> GetCountAsync(string? filterText = null, string? responseStatus = null, bool? notified = null, Guid? calendarEventId = null, Guid? identityUserId = null, CancellationToken cancellationToken = default)
    {
        var query = await GetQueryForNavigationPropertiesAsync();
        query = ApplyFilter(query, filterText, responseStatus, notified, calendarEventId, identityUserId);
        return await query.LongCountAsync(GetCancellationToken(cancellationToken));
    }

    protected virtual IQueryable<CalendarEventParticipant> ApplyFilter(IQueryable<CalendarEventParticipant> query, string? filterText = null, string? responseStatus = null, bool? notified = null)
    {
        return query.WhereIf(!string.IsNullOrWhiteSpace(filterText), e => e.ResponseStatus!.Contains(filterText!)).WhereIf(!string.IsNullOrWhiteSpace(responseStatus), e => e.ResponseStatus.Contains(responseStatus)).WhereIf(notified.HasValue, e => e.Notified == notified);
    }
}