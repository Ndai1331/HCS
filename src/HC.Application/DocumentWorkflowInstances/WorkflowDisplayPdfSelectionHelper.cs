using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HC.DocumentAssignments;
using HC.DocumentFiles;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowDisplayPdfSelectionHelper
{
    public static DocumentFile? SelectBestPdf(
        IReadOnlyList<DocumentFile> files,
        IReadOnlyList<DocumentAssignment> assignments,
        IReadOnlyDictionary<Guid, string> stepTypeByStepId)
    {
        if (files == null || files.Count == 0)
        {
            return null;
        }

        var pdfFiles = files
            .Where(f => IsPdfFile(f) && !string.IsNullOrEmpty(f.Path))
            .ToList();

        if (pdfFiles.Count == 0)
        {
            return null;
        }

        var latestSigned = pdfFiles
            .Where(f => f.IsSigned)
            .OrderByDescending(f => f.UploadedAt)
            .FirstOrDefault();

        if (latestSigned != null)
        {
            return latestSigned;
        }

        var fileById = files.ToDictionary(f => f.Id);
        var doneBlockingAssignments = assignments
            .Where(a =>
                a.Status == nameof(DocumentAssignmentStatus.DONE)
                && a.DocumentFileResultId.HasValue
                && a.WorkflowStepTemplateId.HasValue
                && stepTypeByStepId.TryGetValue(a.WorkflowStepTemplateId.Value, out var stepType)
                && WorkflowStepNavigationHelper.IsBlockingStep(stepType))
            .OrderByDescending(a => a.ProcessedAt);

        foreach (var assignment in doneBlockingAssignments)
        {
            if (fileById.TryGetValue(assignment.DocumentFileResultId!.Value, out var resultFile)
                && IsPdfFile(resultFile)
                && !string.IsNullOrEmpty(resultFile.Path))
            {
                return resultFile;
            }
        }

        return pdfFiles
            .OrderByDescending(f => f.UploadedAt)
            .FirstOrDefault();
    }

    internal static bool IsPdfFile(DocumentFile file)
    {
        var ext = Path.GetExtension(file.Name ?? file.Path ?? "").ToLowerInvariant();
        return ext == ".pdf";
    }
}
