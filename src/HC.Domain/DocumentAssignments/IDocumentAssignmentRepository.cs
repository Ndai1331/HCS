using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Volo.Abp.Domain.Repositories;

namespace HC.DocumentAssignments;

public partial interface IDocumentAssignmentRepository : IRepository<DocumentAssignment, Guid>
{
    Task DeleteAllAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, CancellationToken cancellationToken = default);
    Task<DocumentAssignmentWithNavigationProperties> GetWithNavigationPropertiesAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<DocumentAssignmentWithNavigationProperties>> GetListWithNavigationPropertiesAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<List<DocumentAssignment>> GetListAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, string? sorting = null, int maxResultCount = int.MaxValue, int skipCount = 0, CancellationToken cancellationToken = default);
    Task<long> GetCountAsync(string? filterText = null, int? stepOrderMin = null, int? stepOrderMax = null, string? actionType = null, string? status = null, DateTime? assignedAtMin = null, DateTime? assignedAtMax = null, DateTime? processedAtMin = null, DateTime? processedAtMax = null, bool? isCurrent = null, Guid? documentId = null, Guid? workflowStepTemplateId = null, Guid? receiverUserId = null, CancellationToken cancellationToken = default);
}