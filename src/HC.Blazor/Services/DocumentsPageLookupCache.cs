using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HC.Documents;
using HC.MasterDatas;
using HC.Shared;
using Microsoft.Extensions.Caching.Memory;
using Volo.Abp.Application.Dtos;
using Volo.Abp.DependencyInjection;
using Volo.Abp.MultiTenancy;

namespace HC.Blazor.Services;

public interface IDocumentsPageLookupCache
{
    Task<List<LookupDto<Guid>>> GetMasterDataLookupAsync(string typeValue, Func<Task<PagedResultDto<MasterDataDto>>> loadFactory);

    Task<List<LookupDto<Guid>>> GetUnitsLookupAsync(Func<Task<PagedResultDto<LookupDto<Guid>>>> loadFactory);
}

/// <summary>
/// Scoped cache for document-related master data / unit lookups to reduce duplicate API calls across document pages.
/// </summary>
public class DocumentsPageLookupCache : IDocumentsPageLookupCache, IScopedDependency
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(10);
    private readonly IMemoryCache _memoryCache;
    private readonly ICurrentTenant _currentTenant;

    public DocumentsPageLookupCache(IMemoryCache memoryCache, ICurrentTenant currentTenant)
    {
        _memoryCache = memoryCache;
        _currentTenant = currentTenant;
    }

    private string CacheKey(string suffix)
    {
        var tenant = _currentTenant.Id?.ToString() ?? "host";
        return $"HCS:documents-lookup:{tenant}:{suffix}";
    }

    public async Task<List<LookupDto<Guid>>> GetMasterDataLookupAsync(string typeValue, Func<Task<PagedResultDto<MasterDataDto>>> loadFactory)
    {
        var key = CacheKey($"md-{typeValue}");
        if (!_memoryCache.TryGetValue(key, out List<LookupDto<Guid>>? list))
        {
            var result = await loadFactory();
            list = result.Items.Select(x => new LookupDto<Guid> { Id = x.Id, DisplayName = x.Name }).ToList();
            _memoryCache.Set(key, list, new MemoryCacheEntryOptions { SlidingExpiration = CacheDuration });
        }

        return list!;
    }

    public async Task<List<LookupDto<Guid>>> GetUnitsLookupAsync(Func<Task<PagedResultDto<LookupDto<Guid>>>> loadFactory)
    {
        var key = CacheKey("units");
        if (!_memoryCache.TryGetValue(key, out List<LookupDto<Guid>>? list))
        {
            var result = await loadFactory();
            list = result.Items.ToList();
            _memoryCache.Set(key, list, new MemoryCacheEntryOptions { SlidingExpiration = CacheDuration });
        }

        return list!;
    }
}
