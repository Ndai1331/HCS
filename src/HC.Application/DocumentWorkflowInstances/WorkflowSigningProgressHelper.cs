using System;
using System.Collections.Generic;
using System.Linq;
using HC.DocumentAssignments;
using HC.DocumentFiles;
using HC.WorkflowStepTemplates;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowSigningProgressHelper
{
    /// <summary>
    /// True when the workflow has real signing activity (not merely submit-for-signing).
    /// Blocks initiator cancel/revoke when true.
    /// </summary>
    public static bool HasWorkflowSigningOccurred(
        DocumentWorkflowInstance instance,
        IReadOnlyList<DocumentAssignment> assignmentsForDocument,
        IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepTemplateById,
        IReadOnlyList<DocumentFile>? documentFilesForDocument = null)
    {
        if (HasSignedWorkflowFileSinceStart(instance, documentFilesForDocument))
        {
            return true;
        }

        return HasAnyBlockingStepSigningCompleted(instance, assignmentsForDocument, stepTemplateById);
    }

    /// <summary>
    /// Signed output files on the workflow document (IsSigned=true). Submit/duplicate files stay IsSigned=false.
    /// </summary>
    public static bool HasSignedWorkflowFileSinceStart(
        DocumentWorkflowInstance instance,
        IReadOnlyList<DocumentFile>? documentFilesForDocument)
    {
        if (documentFilesForDocument == null || documentFilesForDocument.Count == 0)
        {
            return false;
        }

        return documentFilesForDocument.Any(f =>
            f.IsSigned
            && f.UploadedAt >= instance.StartedAt);
    }

    /// <summary>
    /// Any SIGN or PROCESS step completed (assignment DONE) in this workflow run.
    /// VIEW-only completion does not count.
    /// </summary>
    public static bool HasAnyBlockingStepSigningCompleted(
        DocumentWorkflowInstance instance,
        IReadOnlyList<DocumentAssignment> assignmentsForDocument,
        IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepTemplateById)
    {
        var blockingStepIds = GetBlockingStepIds(instance, stepTemplateById);
        if (blockingStepIds.Count == 0)
        {
            return false;
        }

        return assignmentsForDocument.Any(a =>
            a.CreationTime >= instance.StartedAt
            && a.WorkflowStepTemplateId.HasValue
            && blockingStepIds.Contains(a.WorkflowStepTemplateId.Value)
            && a.Status == nameof(DocumentAssignmentStatus.DONE));
    }

    private static HashSet<Guid> GetBlockingStepIds(
        DocumentWorkflowInstance instance,
        IReadOnlyDictionary<Guid, WorkflowStepTemplate> stepTemplateById)
    {
        if (stepTemplateById.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var committedIds = DocumentSigningQueryHelper.TryDeserializeCommittedStepTemplateIds(
            instance.CommittedStepTemplateIdsJson);

        IEnumerable<Guid> candidateIds = committedIds is { Count: > 0 }
            ? committedIds
            : stepTemplateById.Keys;

        return candidateIds
            .Where(id => stepTemplateById.TryGetValue(id, out var st)
                && WorkflowStepNavigationHelper.IsBlockingStep(st.Type))
            .ToHashSet();
    }
}
