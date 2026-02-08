using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentWorkflowInstanceFiles;

public partial interface IDocumentWorkflowInstanceFileRepository : IRepository<DocumentWorkflowInstanceFile, Guid>
{
    Task<List<DocumentWorkflowInstanceFile>> GetListByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceFileWithNavigationProperties>> GetListWithNavigationPropertiesByDocumentWorkflowInstanceIdAsync(Guid documentWorkflowInstanceId, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<DocumentWorkflowInstanceFileWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceFileWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, Guid? documentFileId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DocumentWorkflowInstanceFile>> GetListAsync(string? filterText = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, Guid? documentFileId = null, CancellationToken cancellationToken = default);
}