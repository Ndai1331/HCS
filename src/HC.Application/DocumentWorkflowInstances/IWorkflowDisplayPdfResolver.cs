using System;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowDisplayPdfResolver
{
    Task<WorkflowDisplayPdfFileDto?> ResolveAsync(Guid documentId);
}
