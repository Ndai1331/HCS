using HC.CalendarEventParticipants;
using HC.CalendarEvents;
using System;
using System.Linq;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace HC.Projects;

public class ProjectDeletedEventHandler : ILocalEventHandler<EntityDeletedEventData<Project>>, ITransientDependency
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventParticipantRepository _calendarEventParticipantRepository;

    public ProjectDeletedEventHandler(
        ICalendarEventRepository calendarEventRepository,
        ICalendarEventParticipantRepository calendarEventParticipantRepository)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventParticipantRepository = calendarEventParticipantRepository;
    }

    public async Task HandleEventAsync(EntityDeletedEventData<Project> eventData)
    {
        if (eventData.Entity is not ISoftDelete softDeletedEntity || !softDeletedEntity.IsDeleted)
        {
            return;
        }

        try
        {
            var relatedId = eventData.Entity.Id.ToString();
            var calendarEvents = await _calendarEventRepository.GetListAsync(
                x => x.RelatedType == RelatedType.PROJECT.ToString() && x.RelatedId == relatedId);

            if (!calendarEvents.Any())
            {
                return;
            }

            var calendarEventIds = calendarEvents.Select(x => x.Id).ToList();
            var participants = await _calendarEventParticipantRepository.GetListAsync(
                x => calendarEventIds.Contains(x.CalendarEventId));

            if (participants.Any())
            {
                await _calendarEventParticipantRepository.DeleteManyAsync(participants);
            }

            await _calendarEventRepository.DeleteManyAsync(calendarEvents);
        }
        catch
        {
            // Keep delete flow resilient if related calendar cleanup fails.
        }
    }
}
