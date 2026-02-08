using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Input DTO for submitting a document to a workflow
/// </summary>
public class SubmitToWorkflowInput
{
    /// <summary>
    /// Document ID - required when UseWorkflowTemplateFile = false.
    /// When UseWorkflowTemplateFile = true, this can be null (a new document will be created).
    /// </summary>
    public Guid? DocumentId { get; set; }

    [Required]
    public Guid WorkflowId { get; set; }

    /// <summary>
    /// If true, use the workflow template file path to create a new Document + DocumentFile.
    /// The new document will have SourceType = Workflow (3).
    /// </summary>
    public bool UseWorkflowTemplateFile { get; set; }

    /// <summary>
    /// If true, use the workflow template file. If false, use the document's uploaded file.
    /// </summary>
    public bool UseTemplateFile { get; set; }

    /// <summary>
    /// Optional: File ID from user upload to use as signing file
    /// </summary>
    public Guid? DocumentFileId { get; set; }

    /// <summary>
    /// Optional: List of DocumentFile IDs to attach as DocumentWorkflowInstanceFiles
    /// </summary>
    public List<Guid>? AttachedFileIds { get; set; }

    /// <summary>
    /// Signing content/comment - saved to DocumentHistory.Comment
    /// </summary>
    public string? SigningContent { get; set; }
}
