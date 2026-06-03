using System;
using System.Collections.Generic;
using System.Linq;
using HC.DocumentAssignments;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowSigningProgressHelper
{
    public static bool HasAnySignStepCompleted(
        DocumentWorkflowInstance instance,
        IReadOnlyList<DocumentAssignment> assignmentsForDocument,
        IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepTemplateById)
    {
        var committedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(
            instance.CommittedStepTemplateIdsJson);
        if (committedIds == null || committedIds.Count == 0)
        {
            return false;
        }

        var signStepIds = committedIds
            .Where(sid => stepTemplateById.TryGetValue(sid, out var st)
                && string.Equals(st.Type, nameof(WorkflowStepType.SIGN), StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        if (signStepIds.Count == 0)
        {
            return false;
        }

        return assignmentsForDocument.Any(a =>
            a.CreationTime >= instance.StartedAt
            && a.WorkflowStepTemplateId.HasValue
            && signStepIds.Contains(a.WorkflowStepTemplateId.Value)
            && a.Status == nameof(DocumentAssignmentStatus.DONE));
    }
}
