using System;
using System.Collections.Generic;
using HC.Shared;

namespace HC.Documents;

/// <summary>
/// Aggregate payload the Documents Blazor page fetches on first paint so permissions,
/// master-data lookups, units and workflows arrive in a single HTTP round-trip.
/// Replaces the previous ~15 calls (8 permissions + 7 lookups) with 1.
/// </summary>
public class DocumentsPageBootstrapDto
{
    public DocumentsPagePermissionsDto Permissions { get; set; } = new();

    /// <summary>
    /// MasterData lookups keyed by MasterDataType.TypeValue (e.g. "DocumentType", "Field", ...).
    /// Values are already ordered the same way as <c>GetMasterDataLookupAsync</c> returns them.
    /// </summary>
    public Dictionary<string, List<LookupDto<Guid>>> MasterDataLookups { get; set; } = new();

    public List<LookupDto<Guid>> Units { get; set; } = new();

    public List<LookupDto<Guid>> Workflows { get; set; } = new();
}

public class DocumentsPagePermissionsDto
{
    public bool CanCreate { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool CanSend { get; set; }
    public bool CanSubmitForSigning { get; set; }
    public bool CanSubmitForApproval { get; set; }
    public bool CanRejectApproval { get; set; }
    public bool CanApproveWithNote { get; set; }
}

public class GetDocumentsPageBootstrapInput
{
    /// <summary>
    /// MasterData types that should be preloaded. Pass the enum's TypeValue strings
    /// (e.g. "DocumentType", "Status", "Field"). If empty, defaults to the set used
    /// by the Documents list page.
    /// </summary>
    public List<string> MasterDataTypes { get; set; } = new();

    public int LookupPageSize { get; set; } = 200;

    public bool IncludeUnits { get; set; } = true;

    public bool IncludeWorkflows { get; set; } = false;
}
