using System;
using System.Collections.Generic;
using HC.DocumentHistories;
using HC.DocumentWorkflowInstanceFiles;
using HC.DocumentWorkflowInstanceLogss;
using HC.MasterDatas;
using HC.Shared;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// M3 — action bundle for the Signing modal.
/// The modal previously fired 7 parallel HTTP calls on open; this DTO consolidates
/// them into a single response served by <c>GetActionBundleAsync</c>.
/// </summary>
public class WorkflowInstanceActionBundleDto
{
    public DocumentWorkflowInstanceDto? Instance { get; set; }

    /// <summary>Submit info for the instance's workflow, used to resolve the current step detail.</summary>
    public WorkflowSubmitInfoDto? SubmitInfo { get; set; }

    /// <summary>Resolved current-step detail, pre-picked server-side so the client doesn't have to scan the list.</summary>
    public WorkflowStepDetailDto? CurrentStepDetail { get; set; }

    /// <summary>
    /// Next committed step after the current one (sequential workflows only).
    /// Used to show signer selection when approving and multiple candidates exist.
    /// </summary>
    public WorkflowStepDetailDto? NextStepDetail { get; set; }

    public List<WorkflowStepStatusDto> AllStepsWithStatus { get; set; } = new();

    public List<DocumentWorkflowInstanceLogsWithNavigationPropertiesDto> Logs { get; set; } = new();

    public List<DocumentWorkflowInstanceFileWithNavigationPropertiesDto> Files { get; set; } = new();

    public List<DocumentHistoryWithNavigationPropertiesDto> DocumentHistories { get; set; } = new();

    /// <summary>"LOAI_KY" master-data items used to populate the Signing Method dropdown.</summary>
    public List<MasterDataDto> SigningMethods { get; set; } = new();
}

public class GetWorkflowInstanceActionBundleInput
{
    public Guid WorkflowInstanceId { get; set; }

    public Guid DocumentId { get; set; }

    public int SigningMethodsMaxResultCount { get; set; } = 100;
}
