using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.ProjectTasks;

public partial interface IProjectTaskRepository : IRepository<ProjectTask, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, CancellationToken cancellationToken = default);
    Task<ProjectTaskWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ProjectTaskWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, bool onlyParentTasks = false, bool onlyChildTasks = false, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<ProjectTask>> GetListAsync(string? filterText = null, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, bool onlyParentTasks = false, bool onlyChildTasks = false, string? parentTaskId = null, string? code = null, string? title = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? dueDateMin = null, DateTime? dueDateMax = null, string? priority = null, string? status = null, int? progressPercentMin = null, int? progressPercentMax = null, Guid? projectId = null, Guid? userId = null, CancellationToken cancellationToken = default);
}