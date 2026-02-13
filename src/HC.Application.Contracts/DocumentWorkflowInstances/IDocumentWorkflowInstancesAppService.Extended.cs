using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentHistories;
using Volo.Abp.Application.Dtos;

namespace HC.DocumentWorkflowInstances;

public partial interface IDocumentWorkflowInstancesAppService
{
    /// <summary>
    /// Get workflow info (steps, assignments) for a given workflow to display in submit modal
    /// </summary>
    Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId);

    /// <summary>
    /// Submit a document to a workflow - creates workflow instance, first step assignments, and logs
    /// </summary>
    Task<DocumentWorkflowInstanceDto> SubmitToWorkflowAsync(SubmitToWorkflowInput input);

    /// <summary>
    /// Process a workflow action (Approve/Return/Reject)
    /// </summary>
    Task<DocumentWorkflowInstanceDto> ProcessWorkflowActionAsync(WorkflowActionInput input);

    /// <summary>
    /// Get the active workflow instance for a document (if any)
    /// </summary>
    Task<DocumentWorkflowStatusDto?> GetActiveWorkflowStatusAsync(Guid documentId);

    /// <summary>
    /// Get documents with workflow signing for the signing page
    /// Filter modes: All, SentToMe, SentByMe, Following
    /// </summary>
    Task<DocumentSigningPageResultDto> GetDocumentSigningListAsync(GetDocumentSigningListInput input);

    /// <summary>
    /// Get workflow instance logs (with navigation properties) for the action modal
    /// </summary>
    Task<List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto>> GetWorkflowInstanceLogsAsync(Guid workflowInstanceId);

    /// <summary>
    /// Get workflow instance files (with navigation properties) for the action modal
    /// </summary>
    Task<List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto>> GetWorkflowInstanceFilesAsync(Guid workflowInstanceId);

    /// <summary>
    /// Get document histories (with navigation properties) for the action modal
    /// </summary>
    Task<List<DocumentHistoryWithNavigationPropertiesDto>> GetDocumentHistoriesByDocumentIdAsync(Guid documentId);

    /// <summary>
    /// Check if a workflow instance is overdue and handle it.
    /// If overdue: updates Document status to DA_HUY, creates DocumentHistory,
    /// sets instance status to CANCELLED, and creates a log entry.
    /// Also returns whether the current step allows the Return action.
    /// </summary>
    Task<WorkflowOverdueCheckResultDto> CheckAndHandleOverdueAsync(Guid workflowInstanceId);

    /// <summary>
    /// Re-submit a workflow that was previously returned (RETURNED status).
    /// Allows the initiator to edit signing content, re-attach files, and change document/file selection.
    /// Creates a new workflow instance starting from step 1.
    /// </summary>
    Task<DocumentWorkflowInstanceDto> ResubmitReturnedWorkflowAsync(ResubmitReturnedWorkflowInput input);

    /// <summary>
    /// Get info for a returned workflow instance to pre-populate the re-submit modal.
    /// Returns workflow info, original signing content, attached files, etc.
    /// </summary>
    Task<ReturnedWorkflowInfoDto> GetReturnedWorkflowInfoAsync(Guid workflowInstanceId);
}
