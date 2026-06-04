using System;
using System.Threading.Tasks;
using HC.CalendarEventParticipants;
using HC.ProjectMembers;
using HC.ProjectTaskAssignments;
using Volo.Abp.Data;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Guids;
using Volo.Abp.MultiTenancy;

namespace HC.CalendarEvents;

/// <summary>
/// Backfills CalendarEventParticipant rows from existing project members and task assignments.
/// </summary>
public class CalendarEventParticipantBackfillDataSeedContributor : IDataSeedContributor, ITransientDependency
{
    private readonly ICalendarEventRepository _calendarEventRepository;
    private readonly ICalendarEventParticipantRepository _calendarEventParticipantRepository;
    private readonly IProjectMemberRepository _projectMemberRepository;
    private readonly IProjectTaskAssignmentRepository _projectTaskAssignmentRepository;
    private readonly IGuidGenerator _guidGenerator;
    private readonly ICurrentTenant _currentTenant;

    public CalendarEventParticipantBackfillDataSeedContributor(
        ICalendarEventRepository calendarEventRepository,
        ICalendarEventParticipantRepository calendarEventParticipantRepository,
        IProjectMemberRepository projectMemberRepository,
        IProjectTaskAssignmentRepository projectTaskAssignmentRepository,
        IGuidGenerator guidGenerator,
        ICurrentTenant currentTenant)
    {
        _calendarEventRepository = calendarEventRepository;
        _calendarEventParticipantRepository = calendarEventParticipantRepository;
        _projectMemberRepository = projectMemberRepository;
        _projectTaskAssignmentRepository = projectTaskAssignmentRepository;
        _guidGenerator = guidGenerator;
        _currentTenant = currentTenant;
    }

    public async Task SeedAsync(DataSeedContext context)
    {
        await BackfillProjectEventParticipantsAsync();
        await BackfillTaskEventParticipantsAsync();
    }

    private async Task BackfillProjectEventParticipantsAsync()
    {
        var projectEvents = await _calendarEventRepository.GetListAsync(
            relatedType: RelatedType.PROJECT.ToString(),
            eventType: EventType.TASK_ASSIGNED.ToString(),
            maxResultCount: int.MaxValue);

        foreach (var calendarEvent in projectEvents)
        {
            if (!Guid.TryParse(calendarEvent.RelatedId, out var projectId))
            {
                continue;
            }

            var members = await _projectMemberRepository.GetListWithNavigationPropertiesAsync(
                projectId: projectId,
                maxResultCount: int.MaxValue);

            foreach (var member in members)
            {
                await EnsureParticipantAsync(calendarEvent.Id, member.ProjectMember.UserId, calendarEvent.TenantId);
            }
        }
    }

    private async Task BackfillTaskEventParticipantsAsync()
    {
        var taskEvents = await _calendarEventRepository.GetListAsync(
            relatedType: RelatedType.TASK.ToString(),
            eventType: EventType.TASK_ASSIGNED.ToString(),
            maxResultCount: int.MaxValue);

        foreach (var calendarEvent in taskEvents)
        {
            if (!Guid.TryParse(calendarEvent.RelatedId, out var taskId))
            {
                continue;
            }

            var assignments = await _projectTaskAssignmentRepository.GetListWithNavigationPropertiesAsync(
                projectTaskId: taskId,
                maxResultCount: int.MaxValue);

            foreach (var assignment in assignments)
            {
                await EnsureParticipantAsync(calendarEvent.Id, assignment.ProjectTaskAssignment.UserId, calendarEvent.TenantId);
            }
        }
    }

    private async Task EnsureParticipantAsync(Guid calendarEventId, Guid userId, Guid? tenantId)
    {
        var existingCount = await _calendarEventParticipantRepository.GetCountAsync(
            calendarEventId: calendarEventId,
            identityUserId: userId);

        if (existingCount > 0)
        {
            return;
        }

        var participant = new CalendarEventParticipant(
            _guidGenerator.Create(),
            calendarEventId,
            userId,
            ParticipantResponse.INVITED.ToString(),
            false);

        participant.TenantId = tenantId ?? _currentTenant.Id;
        await _calendarEventParticipantRepository.InsertAsync(participant, autoSave: true);
    }
}
