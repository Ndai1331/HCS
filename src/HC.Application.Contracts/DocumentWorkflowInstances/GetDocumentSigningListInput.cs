using System;
using Volo.Abp.Application.Dtos;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Input DTO for the document signing list page
/// </summary>
public class GetDocumentSigningListInput : PagedAndSortedResultRequestDto
{
    public string? FilterText { get; set; }

    /// <summary>
    /// Filter mode: All, SentToMe, SentByMe, Following
    /// </summary>
    public DocumentSigningFilterMode FilterMode { get; set; } = DocumentSigningFilterMode.All;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }
}

/// <summary>
/// Filter modes for the document signing page
/// </summary>
public enum DocumentSigningFilterMode
{
    /// <summary>
    /// All documents with workflow instances
    /// </summary>
    All = 0,

    /// <summary>
    /// Documents where current user has pending assignments
    /// </summary>
    SentToMe = 1,

    /// <summary>
    /// Documents where current user started the workflow
    /// </summary>
    SentByMe = 2,

    /// <summary>
    /// Documents the user is tracking (has any assignment)
    /// </summary>
    Following = 3
}

/// <summary>
/// Result DTO for the document signing page
/// </summary>
public class DocumentSigningPageResultDto
{
    public long TotalCount { get; set; }
    public System.Collections.Generic.List<DocumentSigningItemDto> Items { get; set; } = new();

    /// <summary>
    /// Count for each filter mode
    /// </summary>
    public int AllCount { get; set; }
    public int SentToMeCount { get; set; }
    public int SentByMeCount { get; set; }
    public int FollowingCount { get; set; }
}

/// <summary>
/// DTO for a single item in the signing document list
/// </summary>
public class DocumentSigningItemDto
{
    public Guid DocumentId { get; set; }
    public string? DocumentNo { get; set; }
    public string DocumentTitle { get; set; } = null!;
    public string? StorageNumber { get; set; }
    public DateTime IncommingDate { get; set; }
    public string? StatusName { get; set; }
    public string? TypeName { get; set; }
    public string? WorkflowName { get; set; }

    /// <summary>
    /// Workflow instance info
    /// </summary>
    public Guid? WorkflowInstanceId { get; set; }
    public string? WorkflowStatus { get; set; }
    public string? CurrentStepName { get; set; }
    public int? CurrentStepOrder { get; set; }
    public int? TotalSteps { get; set; }
    public DateTime? WorkflowStartedAt { get; set; }

    /// <summary>
    /// Current user's assignment status for this document
    /// </summary>
    public string? MyAssignmentStatus { get; set; }
    public bool CanAct { get; set; }
    public Guid? MyAssignmentId { get; set; }
}
