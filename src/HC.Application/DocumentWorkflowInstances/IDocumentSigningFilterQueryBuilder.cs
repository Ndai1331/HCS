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
}
