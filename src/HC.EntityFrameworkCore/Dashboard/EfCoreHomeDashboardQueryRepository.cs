using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HC.Projects;
using HC.ProjectTasks;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;

namespace HC.Dashboard;

public class EfCoreHomeDashboardQueryRepository : IHomeDashboardQueryRepository, ITransientDependency
{
    private readonly IProjectRepository _projectRepository;
    private readonly IProjectTaskRepository _projectTaskRepository;

    public EfCoreHomeDashboardQueryRepository(
        IProjectRepository projectRepository,
        IProjectTaskRepository projectTaskRepository)
    {
        _projectRepository = projectRepository;
        _projectTaskRepository = projectTaskRepository;
    }

    public async Task<HomeDashboardQueryResult> GetProjectAndTaskSummaryAsync(
        DateTime filterStart,
        DateTime filterEndExclusive,
        int maxListItems = 200,
        Guid? userId = null,
        CancellationToken cancellationToken = default)
    {
        var totalProjectsCount = await _projectRepository.GetCountAsync(
            userId: userId,
            cancellationToken: cancellationToken);

        var filteredProjectsCount = await _projectRepository.GetCountAsync(
            startDateMin: filterStart,
            startDateMax: filterEndExclusive,
            userId: userId,
            cancellationToken: cancellationToken);

        var filteredProjects = await _projectRepository.GetListWithNavigationPropertiesAsync(
            startDateMin: filterStart,
            startDateMax: filterEndExclusive,
            userId: userId,
            maxResultCount: maxListItems,
            skipCount: 0,
            cancellationToken: cancellationToken);

        var totalTasksCount = await _projectTaskRepository.GetCountAsync(
            startDateMin: filterStart,
            startDateMax: filterEndExclusive,
            userId: userId,
            cancellationToken: cancellationToken);

        var filteredTasks = await _projectTaskRepository.GetListWithNavigationPropertiesAsync(
            startDateMin: filterStart,
            startDateMax: filterEndExclusive,
            userId: userId,
            sorting: "ProjectTask.StartDate DESC",
            maxResultCount: maxListItems,
            skipCount: 0,
            cancellationToken: cancellationToken);

        var tasksByStatus = filteredTasks
            .GroupBy(t => t.ProjectTask.Status)
            .ToDictionary(g => g.Key, g => g.Count());

        return new HomeDashboardQueryResult
        {
            TotalProjectsCount = totalProjectsCount,
            FilteredProjectsCount = filteredProjectsCount,
            FilteredProjects = filteredProjects,
            TotalTasksCount = totalTasksCount,
            TasksByStatus = tasksByStatus,
            FilteredTasks = filteredTasks
        };
    }
}
