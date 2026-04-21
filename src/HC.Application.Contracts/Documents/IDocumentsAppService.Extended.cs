using System;
using System.Threading.Tasks;
using HC.Shared;

namespace HC.Documents;

public partial interface IDocumentsAppService
{
    Task<LookupDto<Guid>?> GetUnitLookupByIdAsync(Guid id);
    Task<bool> IsDocumentNumberDuplicateAsync(string no, Guid? excludeDocumentId = null);
    Task<bool> IsStorageNumberDuplicateAsync(string storageNumber, Guid? excludeDocumentId = null);

    /// <summary>
    /// Returns the document (with nav-properties + UI flags), its files and the first page
    /// of its histories in a single round-trip. Used by the DocumentDetail Blazor page
    /// to replace 3 separate HTTP calls.
    /// </summary>
    Task<DocumentDetailBundleDto> GetDetailBundleAsync(GetDocumentDetailBundleInput input);

    /// <summary>
    /// Returns permissions + preloaded lookups used by the Documents list page on first paint.
    /// Replaces 8 permission checks + 6–7 lookup calls with 1 round-trip.
    /// </summary>
    Task<DocumentsPageBootstrapDto> GetPageBootstrapAsync(GetDocumentsPageBootstrapInput input);

    /// <summary>
    /// Queues approve-with-note (PDF stamp) as a background job. Use with HTTP 202 + SignalR progress.
    /// </summary>
    Task<QueueDocumentBackgroundOperationResultDto> QueueApproveWithNoteAsync(ApproveDocumentWithNoteInput input);

    /// <summary>
    /// Poll status when SignalR is unavailable (fallback).
    /// </summary>
    Task<DocumentBackgroundOperationStatusDto?> GetBackgroundOperationStatusAsync(Guid operationId);
}