using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Read-only workflow instance queries for signing UI (steps, status, logs, action bundle).
/// </summary>
public interface IWorkflowInstanceQueryService
{
    Task<ReturnedWorkflowInfoDto> GetReturnedWorkflowInfoAsync(Guid workflowInstanceId);

    Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId);

    Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId);

    Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(Guid workflowInstanceId);

    Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(Guid workflowInstanceId);

    Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(Guid documentId);

    Task<WorkflowInstanceActionBundleDto> GetActionBundleAsync(GetWorkflowInstanceActionBundleInput input);
}
