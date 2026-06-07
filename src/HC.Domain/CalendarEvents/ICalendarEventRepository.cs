using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.CalendarEvents;

public partial interface ICalendarEventRepository : IRepository<CalendarEvent, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? title = null, string? description = null, DateTime? startTimeMin = null, DateTime? startTimeMax = null, DateTime? endTimeMin = null, DateTime? endTimeMax = null, bool? allDay = null, string? eventType = null, string? location = null, string? relatedType = null, string? relatedId = null, string? visibility = null, Guid? userId = null, CancellationToken cancellationToken = default);
    Task<List<CalendarEvent>> GetListAsync(string? filterText = null, string? title = null, string? description = null, DateTime? startTimeMin = null, DateTime? startTimeMax = null, DateTime? endTimeMin = null, DateTime? endTimeMax = null, bool? allDay = null, string? eventType = null, string? location = null, string? relatedType = null, string? relatedId = null, string? visibility = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? title = null, string? description = null, DateTime? startTimeMin = null, DateTime? startTimeMax = null, DateTime? endTimeMin = null, DateTime? endTimeMax = null, bool? allDay = null, string? eventType = null, string? location = null, string? relatedType = null, string? relatedId = null, string? visibility = null, Guid? userId = null, CancellationToken cancellationToken = default);
}