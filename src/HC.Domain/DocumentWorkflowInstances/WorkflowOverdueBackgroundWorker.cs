using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.DocumentAssignments;
using HC.DocumentHistories;
using HC.Documents;
using HC.DocumentWorkflowInstanceLogss;
using HC.MasterDatas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Volo.Abp.BackgroundWorkers;
using Volo.Abp.Domain.Repositories;
using Volo.Abp.Threading;
using Volo.Abp.Timing;
using Volo.Abp.Uow;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Background worker that periodically checks for overdue workflow instances and cancels them.
/// Replaces the frontend-triggered CheckAndHandleOverdueAsync approach (ISSUE-05 enhancement).
/// 
/// Runs every 5 minutes (configurable via TimerPeriod).
/// For each IN_PROGRESS workflow instance whose FinishedAt has passed:
///   1. Updates instance status to CANCELLED
///   2. Updates document status to DA_HUY
///   3. Creates a document history record
///   4. Creates a workflow instance log
///   5. Revokes all pending assignments
/// </summary>
public class WorkflowOverdueBackgroundWorker : AsyncPeriodicBackgroundWorkerBase
{
    public WorkflowOverdueBackgroundWorker(
        AbpAsyncTimer timer,
        IServiceScopeFactory serviceScopeFactory)
        : base(timer, serviceScopeFactory)
    {
        // Run every 5 minutes (300,000 ms)
        Timer.Period = 5 * 60 * 1000;
    }

    [UnitOfWork]
    protected override async Task DoWorkAsync(PeriodicBackgroundWorkerContext workerContext)
    {
        Logger.LogInformation("[OVERDUE_WORKER] Starting overdue workflow check...");

        var clock = workerContext.ServiceProvider.GetRequiredService<IClock>();
        var instanceRepository = workerContext.ServiceProvider.GetRequiredService<IDocumentWorkflowInstanceRepository>();
        var assignmentRepository = workerContext.ServiceProvider.GetRequiredService<IDocumentAssignmentRepository>();
        var documentRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<Document, Guid>>();
        var masterDataRepository = workerContext.ServiceProvider.GetRequiredService<IRepository<MasterData, Guid>>();
        var historyManager = workerContext.ServiceProvider.GetRequiredService<DocumentHistoryManager>();
        var logsManager = workerContext.ServiceProvider.GetRequiredService<DocumentWorkflowInstanceLogsManager>();

        var now = clock.Now;

        // Find all IN_PROGRESS instances that are overdue
        // FinishedAt > DateTime.MinValue means SLA deadline was set
        // FinishedAt <= now means the deadline has passed
        var overdueInstances = await instanceRepository.GetListAsync(
            x => x.Status == nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS)
            && x.FinishedAt > DateTime.MinValue
            && x.FinishedAt <= now);

        if (!overdueInstances.Any())
        {
            Logger.LogDebug("[OVERDUE_WORKER] No overdue instances found.");
            return;
        }

        Logger.LogInformation("[OVERDUE_WORKER] Found {Count} overdue workflow instances.", overdueInstances.Count);

        var cancelledCount = 0;

        foreach (var instance in overdueInstances)
        {
            try
            {
                // 1. Update instance status to CANCELLED
                instance.Status = nameof(DocumentWorkflowInstanceStatus.CANCELLED);
                instance.FinishedAt = now;
                await instanceRepository.UpdateAsync(instance);

                // 2. Update document status to DA_HUY
                try
                {
                    var document = await documentRepository.GetAsync(instance.DocumentId);
                    var statusCode = DocumentStatusCode.DA_HUY.GetCode();
                    var statusList = await masterDataRepository.GetListAsync(
                        x => x.Code == statusCode && x.Type == MasterDataType.Status.GetTypeValue());
                    var status = statusList.FirstOrDefault();

                    if (status != null)
                    {
                        document.StatusId = status.Id;
                        await documentRepository.UpdateAsync(document);

                        // Mirror cancel status on original manage-documents row when child is a workflow duplicate.
                        if (document.ParentDocumentId.HasValue)
                        {
                            var parent = await documentRepository.GetAsync(document.ParentDocumentId.Value);
                            parent.StatusId = status.Id;
                            await documentRepository.UpdateAsync(parent);
                        }
                    }
                    else
                    {
                        Logger.LogWarning("[OVERDUE_WORKER] MasterData status DA_HUY not found. DocumentId={DocumentId}", instance.DocumentId);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[OVERDUE_WORKER] Error updating document status. DocumentId={DocumentId}", instance.DocumentId);
                }

                // 3. Create document history (toUser = instance creator, who initiated the workflow)
                try
                {
                    if (instance.CreatorId.HasValue)
                    {
                        await historyManager.CreateAsync(
                            instance.DocumentId,
                            null, // fromUser: system action
                            instance.CreatorId.Value, // toUser: notify creator
                            nameof(WorkflowInstanceLogAction.WORKFLOW_CANCELLED),
                            "Hết hạn xử lý tài liệu - Tự động hủy bởi hệ thống"
                        );
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[OVERDUE_WORKER] Error creating document history. InstanceId={InstanceId}", instance.Id);
                }

                // 4. Create workflow instance log
                try
                {
                    await logsManager.CreateAsync(
                        instance.Id,
                        null, // No assignment
                        null, // System user
                        nameof(WorkflowInstanceLogAction.WORKFLOW_CANCELLED),
                        WorkflowConstants.RoleSystem,
                        nameof(DocumentWorkflowInstanceStatus.IN_PROGRESS),
                        nameof(DocumentWorkflowInstanceStatus.CANCELLED),
                        "Hết hạn xử lý - Tự động hủy bởi hệ thống"
                    );
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[OVERDUE_WORKER] Error creating workflow log. InstanceId={InstanceId}", instance.Id);
                }

                // 5. Revoke all pending assignments for this document's workflow
                try
                {
                    var pendingAssignments = await assignmentRepository.GetListAsync(
                        x => x.DocumentId == instance.DocumentId
                        && x.IsCurrent
                        && x.Status == nameof(DocumentAssignmentStatus.PENDING));

                    foreach (var assignment in pendingAssignments)
                    {
                        assignment.Status = nameof(DocumentAssignmentStatus.REVOKE);
                        assignment.ProcessedAt = now;
                        assignment.IsCurrent = false;
                        await assignmentRepository.UpdateAsync(assignment);
                    }

                    if (pendingAssignments.Any())
                    {
                        Logger.LogInformation("[OVERDUE_WORKER] Revoked {Count} pending assignments for instance {InstanceId}",
                            pendingAssignments.Count, instance.Id);
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "[OVERDUE_WORKER] Error revoking assignments. InstanceId={InstanceId}", instance.Id);
                }

                cancelledCount++;
                Logger.LogInformation("[OVERDUE_WORKER] Cancelled overdue workflow. InstanceId={InstanceId}, DocumentId={DocumentId}",
                    instance.Id, instance.DocumentId);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[OVERDUE_WORKER] Error processing overdue instance {InstanceId}", instance.Id);
                // Continue with next instance
            }
        }

        Logger.LogInformation("[OVERDUE_WORKER] Completed. Cancelled {Count}/{Total} overdue workflows.",
            cancelledCount, overdueInstances.Count);
    }
}
