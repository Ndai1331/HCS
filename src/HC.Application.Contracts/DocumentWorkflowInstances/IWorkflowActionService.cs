using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Workflow approve, return, and reject actions.
/// </summary>
public interface IWorkflowActionService
{
    Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input);
}
