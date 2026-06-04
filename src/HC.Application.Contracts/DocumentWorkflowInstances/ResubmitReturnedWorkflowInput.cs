using System;
using System.Collections.Generic;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Input DTO for re-submitting a workflow that was previously returned (RETURNED status).
/// Allows the initiator to edit signing content, re-attach files, change document/file selection.
/// </summary>
public class ResubmitReturnedWorkflowInput
{
    /// <summary>
    /// The returned workflow instance ID (must have status = RETURNED)
    /// </summary>
    public Guid ReturnedWorkflowInstanceId { get; set; }

    /// <summary>
    /// If true, re-create document file from workflow template
    /// </summary>
    public bool UseWorkflowTemplateFile { get; set; }

    /// <summary>
    /// Optional: New document file ID (user re-uploaded or selected a different file)
    /// </summary>
    public Guid? DocumentFileId { get; set; }

    /// <summary>
    /// Optional: Switch to a different personal document
    /// </summary>
    public Guid? NewDocumentId { get; set; }

    /// <summary>
    /// Updated signing content/comment
    /// </summary>
    public string? SigningContent { get; set; }

    /// <summary>
    /// Optional: List of new DocumentFile IDs to attach
    /// </summary>
    public List<Guid>? AttachedFileIds { get; set; }

    /// <summary>
    /// Optional: List of old file IDs to delete
    /// </summary>
    public List<Guid>? DeleteFileIds { get; set; }

    /// <summary>
    /// Per-step signer selection when role-based assignment resolves multiple candidates.
    /// </summary>
    public List<WorkflowStepSignerSelectionDto> StepSignerSelections { get; set; } = new();

    public List<WorkflowStepViewScopeSelectionDto> ViewStepScopeSelections { get; set; } = new();
}

/// <summary>
/// DTO for returned workflow info, used to pre-populate the re-submit modal
/// </summary>
public class ReturnedWorkflowInfoDto
{
    public Guid WorkflowInstanceId { get; set; }
    public Guid DocumentId { get; set; }
    public Guid WorkflowId { get; set; }
    public string? DocumentTitle { get; set; }
    public string? DocumentNo { get; set; }
    public string? StorageNumber { get; set; }
    public string? LastSigningContent { get; set; }
    public WorkflowSubmitInfoDto WorkflowInfo { get; set; } = null!;

    /// <summary>
    /// Files uploaded by user as additional attachments (from DocumentWorkflowInstanceFile).
    /// These can be removed by the user during re-submit.
    /// </summary>
    public List<AttachedFileDto> AttachedFiles { get; set; } = new();

    /// <summary>
    /// Document's own files (from DocumentFile where DocumentId = instance.DocumentId).
    /// Read-only display to show user what signing files exist on the document.
    /// </summary>
    public List<AttachedFileDto> DocumentFiles { get; set; } = new();
}

/// <summary>
/// DTO for an attached file in the returned workflow info
/// </summary>
public class AttachedFileDto
{
    public Guid FileId { get; set; }
    public string? FileName { get; set; }
    public string? FilePath { get; set; }
}
