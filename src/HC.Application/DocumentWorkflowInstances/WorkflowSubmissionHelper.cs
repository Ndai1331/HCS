using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace HC.DocumentWorkflowInstances;

internal static class WorkflowSubmissionHelper
{
    public static string? SerializeCommittedStepTemplateIds(IReadOnlyList<Guid> orderedStepIds)
    {
        if (orderedStepIds == null || orderedStepIds.Count == 0)
        {
            return null;
        }

        return JsonSerializer.Serialize(orderedStepIds);
    }

    public static bool IsWordFormatPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        var ext = Path.GetExtension(path);
        return string.Equals(ext, ".doc", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ext, ".docx", StringComparison.OrdinalIgnoreCase);
    }
}
