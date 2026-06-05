using System;

namespace HC.DocumentWorkflowInstances;

/// <summary>
/// Input for exporting the document signing list to Excel (same filters as the signing page).
/// </summary>
public class DocumentSigningExcelDownloadDto
{
    public string DownloadToken { get; set; } = null!;

    public string? FilterText { get; set; }

    public DocumentSigningFilterMode FilterMode { get; set; } = DocumentSigningFilterMode.All;

    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public DocumentSigningDateFilterField DateFilterField { get; set; } = DocumentSigningDateFilterField.IncomingDate;

    public Guid? SubmitterUserId { get; set; }

    public Guid? SubmitterOrganizationUnitId { get; set; }
}
