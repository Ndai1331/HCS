using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Projects;

public partial interface IProjectRepository : IRepository<Project, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? code = null, string? name = null, string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null, DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null, Guid? ownerDepartmentId = null, CancellationToken cancellationToken = default);
    Task<ProjectWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ProjectWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? code = null, string? name = null,
     string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null,
      DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null,
       Guid? ownerDepartmentId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    // Task<List<Project>> GetListAsync(string? filterText = null, string? code = null, string? name = null,
    //  string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null,
    //   DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null,
    //    Guid? ownerDepartmentId = null, Guid? userId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);


    Task<long> GetCountAsync(string? filterText = null, string? code = null, string? name = null,
     string? description = null, DateTime? startDateMin = null, DateTime? startDateMax = null,
      DateTime? endDateMin = null, DateTime? endDateMax = null, string? status = null,
       Guid? ownerDepartmentId = null, Guid? userId = null, CancellationToken cancellationToken = default);
}