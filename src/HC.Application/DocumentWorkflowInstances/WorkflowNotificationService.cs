using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.MasterDatas;
using HC.Notifications;
using HC.NotificationReceivers;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.EventBus.Distributed;
using Microsoft.Extensions.Logging;

namespace HC.DocumentWorkflowInstances;

public class WorkflowNotificationService : HCAppService, IWorkflowNotificationService, ITransientDependency
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationReceiverRepository _notificationReceiverRepository;
    private readonly IDistributedEventBus _distributedEventBus;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;

    public WorkflowNotificationService(
        INotificationRepository notificationRepository,
        INotificationReceiverRepository notificationReceiverRepository,
        IDistributedEventBus distributedEventBus,
        IRepository<Document, Guid> documentRepository,
        IRepository<MasterData, Guid> masterDataRepository)
    {
        _notificationRepository = notificationRepository;
        _notificationReceiverRepository = notificationReceiverRepository;
        _distributedEventBus = distributedEventBus;
        _documentRepository = documentRepository;
        _masterDataRepository = masterDataRepository;
    }

    public async Task SendWorkflowNotificationAsync(
        Document document,
        List<Guid> receiverUserIds,
        string titleKey,
        string contentKey)
    {
        try
        {
            var notification = new Notification(
                GuidGenerator.Create(),
                titleKey,
                contentKey,
                SourceType.WORKFLOW.ToString(),
                EventType.WORKFLOW_ACTION.ToString(),
                RelatedType.WORKFLOW.ToString(),
                WorkflowConstants.PriorityHigh,
                document.Id.ToString());
            notification.TenantId = CurrentTenant.Id;
            await _notificationRepository.InsertAsync(notification);

            foreach (var userId in receiverUserIds)
            {
                var receiver = new NotificationReceiver(
                    GuidGenerator.Create(),
                    notification.Id,
                    userId,
                    false);
                receiver.TenantId = CurrentTenant.Id;
                await _notificationReceiverRepository.InsertAsync(receiver);
            }

            await _distributedEventBus.PublishAsync(
                new NotificationCreatedEto
                {
                    NotificationId = notification.Id,
                    ReceiverUserIds = receiverUserIds
                });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error sending workflow notification for document {DocumentId}", document.Id);
        }
    }

    public async Task UpdateDocumentStatusAsync(Guid documentId, DocumentStatusCode statusCode)
    {
        try
        {
            var document = await _documentRepository.GetAsync(documentId);
            if (!await TryApplyDocumentStatusByCodeAsync(document, statusCode))
            {
                return;
            }

            await _documentRepository.UpdateAsync(document);

            if (document.ParentDocumentId.HasValue)
            {
                var parent = await _documentRepository.GetAsync(document.ParentDocumentId.Value);
                if (await TryApplyDocumentStatusByCodeAsync(parent, statusCode))
                {
                    await _documentRepository.UpdateAsync(parent);
                }
            }

            Logger.LogInformation(
                "Document status updated to {Code}: DocumentId={DocumentId}, StatusId={StatusId}",
                statusCode.GetCode(), documentId, document.StatusId);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error updating document status to {Code}: DocumentId={DocumentId}",
                statusCode.GetCode(), documentId);
        }
    }

    private async Task<bool> TryApplyDocumentStatusByCodeAsync(Document document, DocumentStatusCode statusCode)
    {
        var code = statusCode.GetCode();
        var statusQuery = (await _masterDataRepository.GetQueryableAsync())
            .Where(x => x.Code == code && x.Type == MasterDataType.Status.GetTypeValue());
        var status = await AsyncExecuter.FirstOrDefaultAsync(statusQuery);
        if (status == null)
        {
            Logger.LogWarning(
                "MasterData with Code='{Code}' and Type='TRANG_THAI_VB' not found. Document status will not be updated.",
                code);
            return false;
        }

        document.StatusId = status.Id;
        return true;
    }
}
