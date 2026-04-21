using System.Collections.Generic;
using HC.Shared;
using System;

namespace HC.Documents;

/// <summary>
/// Cached page of LookupDto results for the Documents-module dropdowns.
/// Used for MasterData / Unit / Workflow / Department lookups when the caller
/// does not pass a filter (the 99% case on first paint).
/// </summary>
public class DocumentsLookupCacheItem
{
    public long TotalCount { get; set; }
    public List<LookupDto<Guid>> Items { get; set; } = new();
}
