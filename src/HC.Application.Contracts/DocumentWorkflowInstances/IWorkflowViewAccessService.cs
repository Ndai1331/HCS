using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowViewAccessService
{
    Task<bool> CanUserViewWorkflowDocumentAsync(Guid workflowInstanceId, Guid userId);

    Task<HashSet<Guid>> GetViewEligibleDocumentIdsAsync(Guid userId);
}
