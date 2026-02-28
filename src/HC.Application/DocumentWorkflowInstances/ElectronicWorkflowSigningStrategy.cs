using System;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.SignatureSettings;
using Volo.Abp.DependencyInjection;

namespace HC.DocumentWorkflowInstances;


public interface IWorkflowSigningStrategy
{
    string MethodCode { get; }

    Task ApplyAsync(DocumentAssignment assignment, DocumentWorkflowInstance instance, string? noteContent, Guid? selectedUserSignatureId);
}


public sealed class ElectronicWorkflowSigningStrategy : IWorkflowSigningStrategy, ITransientDependency
{
    private readonly IWorkflowSigningExecutionService _signingExecutionService;

    public string MethodCode => nameof(SignType.ELECTRONIC);

    public ElectronicWorkflowSigningStrategy(IWorkflowSigningExecutionService signingExecutionService)
    {
        _signingExecutionService = signingExecutionService;
    }

    public Task ApplyAsync(DocumentAssignment assignment, DocumentWorkflowInstance instance, string? noteContent, Guid? selectedUserSignatureId)
    {
        return _signingExecutionService.ApplyElectronicSignatureAsync(assignment, instance, noteContent, selectedUserSignatureId);
    }
}
