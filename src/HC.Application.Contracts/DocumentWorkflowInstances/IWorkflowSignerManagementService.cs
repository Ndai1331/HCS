using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowSignerManagementService
{
    Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input);
}
