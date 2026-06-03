using System;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowOverdueExtensionService
{
    Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId);

    Task ExtendWorkflowAsync(ExtendWorkflowInput input);

    Task<WorkflowExtensionSummaryDto> GetWorkflowExtensionSummaryAsync(Guid workflowInstanceId);
}
