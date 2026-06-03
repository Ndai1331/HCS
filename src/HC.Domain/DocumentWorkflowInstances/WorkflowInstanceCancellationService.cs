using System;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstanceLogss;
using HC.Documents;
using HC.MasterDatas;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Domain.Services;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Shared cancellation logic for workflow instances (overdue auto-cancel and initiator revoke).
/// </summary>
public class WorkflowInstanceCancellationService : DomainService
{
    private readonly IDocumentWorkflowInstanceRepository _instanceRepository;
    private readonly IDocumentAssignmentRepository _assignmentRepository;
    private readonly IRepository<Document, Guid> _documentRepository;
    private readonly IRepository<MasterData, Guid> _masterDataRepository;
    private readonly DocumentHistoryManager _historyManager;
    private readonly DocumentWorkflowInstanceLogsManager _logsManager;

    public WorkflowInstanceCancellationService(
        IDocumentWorkflowInstanceRepository instanceRepository,
        IDocumentAssignmentRepository assignmentRepository,
        IRepository<Document, Guid> documentRepository,
        IRepository<MasterData, Guid> masterDataRepository,
        DocumentHistoryManager historyManager,
        DocumentWorkflowInstanceLogsManager logsManager)
    {
        _instanceRepository = instanceRepository;
        _assignmentRepository = assignmentRepository;
        _documentRepository = documentRepository;
        _masterDataRepository = masterDataRepository;
        _historyManager = historyManager;
        _logsManager = logsManager;
    }

    public async Task CancelInstanceAsync(
        DocumentWorkflowInstance instance,
        DateTime now,
        Guid? historyActorUserId,
        string historyNote,
        string logNote,
        string logFromStatus,
        string logRole,
        ILogger logger)
    {
        instance.Status = nameof(DocumentWorkflowInstanceStatus.CANCELLED);
        instance.FinishedAt = now;
        await _instanceRepository.UpdateAsync(instance);

        try
        {
            var document = await _documentRepository.GetAsync(instance.DocumentId);
            var statusCode = DocumentStatusCode.DA_HUY.GetCode();
            var statusList = await _masterDataRepository.GetListAsync(
                x => x.Code == statusCode && x.Type == MasterDataType.Status.GetTypeValue());
            var status = statusList.FirstOrDefault();

            if (status != null)
            {
                document.StatusId = status.Id;
                await _documentRepository.UpdateAsync(document);

                if (document.ParentDocumentId.HasValue)
                {
                    var parent = await _documentRepository.GetAsync(document.ParentDocumentId.Value);
                    parent.StatusId = status.Id;
                    await _documentRepository.UpdateAsync(parent);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error updating document status on workflow cancel. DocumentId={DocumentId}", instance.DocumentId);
        }

        try
        {
            if (historyActorUserId.HasValue)
            {
                await _historyManager.CreateAsync(
                    instance.DocumentId,
                    null,
                    historyActorUserId.Value,
                    nameof(WorkflowInstanceLogAction.WORKFLOW_CANCELLED),
                    historyNote);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating document history on workflow cancel. InstanceId={InstanceId}", instance.Id);
        }

        try
        {
            await _logsManager.CreateAsync(
                instance.Id,
                null,
                null,
                nameof(WorkflowInstanceLogAction.WORKFLOW_CANCELLED),
                logRole,
                logFromStatus,
                nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                logNote);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating workflow log on cancel. InstanceId={InstanceId}", instance.Id);
        }

        try
        {
            var pendingAssignments = await _assignmentRepository.GetListAsync(
                x => x.DocumentId == instance.DocumentId
                     && x.IsCurrent
                     && x.Status == nameof(DocumentAssignmentStatus.PENDING));

            foreach (var assignment in pendingAssignments)
            {
                assignment.Status = nameof(DocumentAssignmentStatus.REVOKE);
                assignment.ProcessedAt = now;
                assignment.IsCurrent = false;
                await _assignmentRepository.UpdateAsync(assignment);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking assignments on workflow cancel. InstanceId={InstanceId}", instance.Id);
        }
    }
}
