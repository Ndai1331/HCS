using System;

namespace HC.DocumentWorkflowInstances;

public abstract class DocumentWorkflowInstanceDownloadTokenCacheItemBase
{
    public string Token { get; set; } = null!;

    /// <summary>
    /// User who requested the export (required for user-scoped signing Excel export on anonymous GET).
    /// </summary>
    public Guid? UserId { get; set; }
}