using System;
using System.Threading.Tasks;

namespace HC.DocumentWorkflowInstances;

public interface IDocumentSigningFilterQueryBuilder
{
    Task<SigningFilterState> BuildSigningFilterStateAsync(
        Guid currentUserId,
        string? filterText,
        DocumentSigningFilterMode filterMode,
        DocumentSigningDateFilterField dateFilterField,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? focusDocumentId,
        Guid? submitterUserId = null,
        Guid? submitterOrganizationUnitId = null);

    /// <summary>
    /// Build signing filter state across all users (admin export scope).
    /// </summary>
    Task<SigningFilterState> BuildAllUsersSigningFilterStateAsync(
        string? filterText,
        DocumentSigningFilterMode filterMode,
        DocumentSigningDateFilterField dateFilterField,
        DateTime? fromDate,
        DateTime? toDate,
        Guid? focusDocumentId,
        Guid? submitterUserId = null,
        Guid? submitterOrganizationUnitId = null);
}
