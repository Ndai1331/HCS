using System.Threading.Tasks;
using HC.DocumentWorkflowInstances;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Read-side queries for the document signing list (extracted from DocumentWorkflowInstancesAppService).
/// </summary>
public interface IDocumentSigningQueryService
{
    Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input);
}
