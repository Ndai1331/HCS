using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HC.WorkflowStepAssignments;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

public interface IWorkflowSubmitInfoQueryService
{
    Task<bool> IsDocumentSourceFileWordFormatAsync(Guid documentId);

    Task<WorkflowSubmitInfoDto> GetWorkflowSubmitInfoAsync(Guid workflowId);

    Task<WorkflowStepDetailDto> BuildWorkflowStepDetailAsync(
        WorkflowStepTemplate step,
        IReadOnlyList<WorkflowStepAssignment> stepAssignments,
        Guid submitterUserId);
}
