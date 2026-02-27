using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.SignatureSettings;
using Volo.Abp.DependencyInjection;

namespace HC.DocumentWorkflowInstances;

public sealed class DigitalWorkflowSigningStrategy : IWorkflowSigningStrategy, ITransientDependency
{
    private readonly IWorkflowSigningExecutionService _signingExecutionService;

    public string MethodCode => nameof(SignType.DIGITAL);

    public DigitalWorkflowSigningStrategy(IWorkflowSigningExecutionService signingExecutionService)
    {
        _signingExecutionService = signingExecutionService;
    }

    public Task ApplyAsync(DocumentAssignment assignment, DocumentWorkflowInstance instance, string? noteContent)
    {
        return _signingExecutionService.ApplyDigitalSignatureAsync(assignment, instance, noteContent);
    }
}
