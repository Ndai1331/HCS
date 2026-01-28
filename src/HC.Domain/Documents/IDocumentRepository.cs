using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.Documents;

public partial interface IDocumentRepository : IRepository<Document, Guid>
{
    Task DeleteAllAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, CancellationToken cancellationToken = default);
    Task<DocumentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<Document>> GetListAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, string? no = null, string? title = null, string? currentStatus = null, DateTime? completedTimeMin = null, DateTime? completedTimeMax = null, string? storageNumber = null, DateTime? incommingDateMin = null, DateTime? incommingDateMax = null, Guid? fieldId = null, Guid? unitId = null, Guid? workflowId = null, Guid? statusId = null, Guid? typeId = null, Guid? urgencyLevelId = null, Guid? secrecyLevelId = null, Guid? creatorId = null, CancellationToken cancellationToken = default);
}