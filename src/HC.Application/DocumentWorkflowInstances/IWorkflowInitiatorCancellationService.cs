using System.Threading.Tasks;
using Volo.Abp.DependencyInjection;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowInitiatorCancellationService : ITransientDependency
{
    Task CancelWorkflowByInitiatorAsync(CancelWorkflowByInitiatorInput input);
}
