using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstanceLogss;

public partial interface IDocumentWorkflowInstanceLogsRepository : IRepository<DocumentWorkflowInstanceLogs, Guid>
{
    Task<List<DocumentWorkflowInstanceLogs>> GetListByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceLogsWithNavigationProperties>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<DocumentWorkflowInstanceLogsWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceLogsWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, Guid? documentAssignmentId = null, Guid? actorUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceLogs>> GetListAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? action = null, string? actorRole = null, string? fromStatus = null, string? toStatus = null, string? note = null, Guid? documentAssignmentId = null, Guid? actorUserId = null, CancellationToken cancellationToken = default);
}