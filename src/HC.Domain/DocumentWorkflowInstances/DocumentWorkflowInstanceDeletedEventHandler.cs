using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstances;
using System.Threading.Tasks;
using Volo.Abp;
using Volo.Abp.DependencyInjection;
using Volo.Abp.Domain.Entities.Events;
using Volo.Abp.EventBus;

namespace HC.DocumentWorkflowInstances;

public class DocumentWorkflowInstanceDeletedEventHandler : ILocalEventHandler<EntityDeletedEventData<DocumentWorkflowInstance>>, ITransientDependency
{
    private readonly IDocumentWorkflowInstanceFileRepository _documentWorkflowInstanceFileRepository;
    private readonly IDocumentWorkflowInstanceLogsRepository _documentWorkflowInstanceLogsRepository;

    public DocumentWorkflowInstanceDeletedEventHandler(IDocumentWorkflowInstanceFileRepository documentWorkflowInstanceFileRepository, IDocumentWorkflowInstanceLogsRepository documentWorkflowInstanceLogsRepository)
    {
        _documentWorkflowInstanceFileRepository = documentWorkflowInstanceFileRepository;
        _documentWorkflowInstanceLogsRepository = documentWorkflowInstanceLogsRepository;
    }

    public async Task HandleEventAsync(EntityDeletedEventData<DocumentWorkflowInstance> eventData)
    {
        if (eventData.Entity is not ISoftDelete softDeletedEntity)
        {
            return;
        }

        if (!softDeletedEntity.IsDeleted)
        {
            return;
        }

        try
        {
            await _documentWorkflowInstanceFileRepository.DeleteManyAsync(await _documentWorkflowInstanceFileRepository.GetListByDocumentWorkflowInstanceIdAsync(eventData.Entity.Id));
            await _documentWorkflowInstanceLogsRepository.DeleteManyAsync(await _documentWorkflowInstanceLogsRepository.GetListByDocumentWorkflowInstanceIdAsync(eventData.Entity.Id));
        }
        catch
        {
            //...
        }
    }
}