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

public class EfCoreCalendarEventParticipantRepository : EfCoreCalendarEventParticipantRepositoryBase, ICalendarEventParticipantRepository
{
    public EfCoreCalendarEventParticipantRepository(IDbContextProvider<HCDbContext> dbContextProvider) : base(dbContextProvider)
    {
    }

    public virtual async Task<IReadOnlyList<(Guid CalendarEventId, int Count)>> GetCountsByCalendarEventIdsAsync(
        IReadOnlyList<Guid> calendarEventIds,
        CancellationToken cancellationToken = default)
    {
        if (calendarEventIds == null || calendarEventIds.Count == 0)
        {
            return Array.Empty<(Guid, int)>();
        }

        var distinctIds = calendarEventIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return Array.Empty<(Guid, int)>();
        }

        var dbSet = await GetDbSetAsync();
        var rows = await dbSet
            .AsNoTracking()
            .Where(p => distinctIds.Contains(p.CalendarEventId))
            .GroupBy(p => p.CalendarEventId)
            .Select(g => new { CalendarEventId = g.Key, Count = g.Count() })
            .ToListAsync(GetCancellationToken(cancellationToken));

        return rows.Select(r => (r.CalendarEventId, r.Count)).ToList();
    }
}