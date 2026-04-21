using System;

namespace HC.Documents.BackgroundJobs;

[Serializable]
public class ApproveWithNoteBackgroundJobArgs
{
    public Guid OperationId { get; set; }
}
