using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.DocumentWorkflowInstanceLogss;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentHistories;
using Volo.Abp.Application.Dtos;
using Volo.Abp.Content;

namespace HC.DocumentWorkflowInstances;

public partial interface IDocumentWorkflowInstancesAppService
{
    /// <summary>
    /// Get workflow info (steps, assignments) for a given workflow to display in submit modal
    /// </summary>
    Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId);

    /// <summary>
    /// Returns true if the document's first file is .doc or .docx.
    /// Used to determine if SigningContent is required when submitting with "my document".
    /// </summary>
    Task<bool> IsDocumentSourceFileWordFormatAsync(Guid documentId);


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
    /// Export the signing list to Excel (all rows matching filters, not paged).
    /// </summary>
    Task<IRemoteStreamContent> GetDocumentSigningListAsExcelFileAsync(DocumentSigningExcelDownloadDto input);

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

    /// <summary>
    /// Get all workflow steps with their signing status for a given workflow instance.
    /// Shows each step, assigned users, and whether they have signed (with signing index).
    /// Used in the action modal to display full workflow progress.
    /// </summary>
    Task<List<WorkflowStepStatusDto>> GetAllStepsWithStatusAsync(Guid workflowInstanceId);

    /// <summary>
    /// M3 — bundles all data the Signing modal needs on open (instance, submit info, logs,
    /// files, histories, all-step status, signing methods) into one round-trip.
    /// </summary>
    Task<WorkflowInstanceActionBundleDto> GetActionBundleAsync(GetWorkflowInstanceActionBundleInput input);

    /// <summary>
    /// Workflow creator updates pending signers for steps that have not been completed yet.
    /// </summary>
    Task UpdateWorkflowStepSignersAsync(UpdateWorkflowStepSignersInput input);

    /// <summary>
    /// Extend signing deadline (business days). Allowed for current-step signer or ADMIN/admin role.
    /// </summary>
    Task ExtendWorkflowAsync(ExtendWorkflowInput input);

    Task<WorkflowExtensionSummaryDto> GetWorkflowExtensionSummaryAsync(Guid workflowInstanceId);
}
