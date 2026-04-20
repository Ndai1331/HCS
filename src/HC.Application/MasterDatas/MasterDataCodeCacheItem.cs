using System;

namespace HC.MasterDatas;

/// <summary>
/// Caches the resolved MasterData Id for a given (Type, Code) pair so frequently-used lookups
/// such as document status code → status id stay out of the database hot path.
/// </summary>
public class MasterDataCodeCacheItem
{
    public Guid Id { get; set; }
    public string? Code { get; set; }
    public string? Type { get; set; }
}
