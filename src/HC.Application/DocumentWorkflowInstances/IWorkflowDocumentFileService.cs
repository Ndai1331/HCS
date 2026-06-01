using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowDocumentFileService
{
    Task<Guid?> CopyDocumentFileForNextStepAsync(Guid? sourceFileId, Guid documentId);

    Task<Dictionary<Guid, Guid>> DuplicateDocumentFilesForWorkflowAsync(Guid sourceDocumentId, Guid targetDocumentId);
}
