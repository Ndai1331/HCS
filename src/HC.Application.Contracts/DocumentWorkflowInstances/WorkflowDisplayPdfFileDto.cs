using System;

namespace HC.DocumentWorkflowInstances;

public class WorkflowDisplayPdfFileDto
{
    public Guid DocumentFileId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public bool IsSigned { get; set; }

    public DateTime UploadedAt { get; set; }
}
