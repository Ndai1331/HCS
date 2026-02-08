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
}
