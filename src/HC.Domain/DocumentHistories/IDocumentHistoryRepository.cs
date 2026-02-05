using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentHistories;

public partial interface IDocumentHistoryRepository : IRepository<DocumentHistory, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, CancellationToken cancellationToken = default);
    Task<DocumentHistoryWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentHistoryWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DocumentHistory>> GetListAsync(string? filterText = null, string? comment = null, string? action = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? comment = null, string? action = null, Guid? documentId = null, Guid? fromUser = null, Guid? toUser = null, CancellationToken cancellationToken = default);

        Task<List<DocumentHistoryWithNavigationProperties>> GetHistoryByDocumentIdAsync(
        Guid documentId,
        int skipCount,
        int maxResultCount,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Get count of document histories by document ID
    /// </summary>
    Task<long> GetCountByDocumentIdAsync(
        Guid documentId,
        CancellationToken cancellationToken = default);
}