using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Workflow submission and resubmit operations (facade over DocumentWorkflowInstancesAppService).
/// </summary>
public interface IWorkflowSubmissionService
{
    Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input);

    Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input);
}
