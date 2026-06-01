using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.Documents;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowNotificationService
{
    Task SendWorkflowNotificationAsync(Document document, List<Guid> receiverUserIds, string titleKey, string contentKey);

    Task UpdateDocumentStatusAsync(Guid documentId, DocumentStatusCode statusCode);
}
