using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Volo.Abp.Domain.Services;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Overdue workflow cancellation (background worker and grace expiry).
/// </summary>
public class WorkflowOverdueCancellationService : DomainService
{
    private readonly WorkflowInstanceCancellationService _cancellationService;

    public WorkflowOverdueCancellationService(WorkflowInstanceCancellationService cancellationService)
    {
        _cancellationService = cancellationService;
    }

    public Task CancelOverdueInstanceAsync(DocumentWorkflowInstance instance, DateTime now, ILogger logger)
    {
        return _cancellationService.CancelInstanceAsync(
            instance,
            now,
            instance.CreatorId,
            "Hết hạn xử lý tài liệu - Tự động hủy bởi hệ thống",
            "Hết hạn xử lý - Tự động hủy sau thời gian ân hạn",
            nameof(DocumentWorkflowInstanceStatus.OVERDUE),
            WorkflowConstants.RoleSystem,
            logger);
    }
}
