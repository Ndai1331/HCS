using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Workflow submission and resubmit operations.
/// </summary>
public interface IWorkflowSubmissionService
{
    Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input);

    Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input);
}
