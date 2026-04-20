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
using Volo.Abp.Domain.Entities;
using Volo.Abp.Http.Client;
using Volo.Abp.MultiTenancy;

namespace HC.Blazor.Services;

public interface IDocumentsPageLookupCache
{
    Task<List<LookupDto<Guid>>> GetMasterDataLookupAsync(string typeValue, Func<Task<PagedResultDto<MasterDataDto>>> loadFactory);

    Task<List<LookupDto<Guid>>> GetUnitsLookupAsync(Func<Task<PagedResultDto<LookupDto<Guid>>>> loadFactory);

    /// <summary>
    /// Caches single master data row by id (display labels on detail/view pages, revisits within the same Blazor circuit).
    /// </summary>
    Task<LookupDto<Guid>?> GetMasterDataByIdAsync(Guid id, Func<Task<MasterDataDto>> loadFactory);

    /// <summary>
    /// Pre-seed an already-known lookup pair so subsequent calls hit the cache (used when nav-prop bundles already carry name).
    /// </summary>
    void SetMasterDataById(LookupDto<Guid> lookup);

    /// <summary>
    /// Pre-seed unit lookup pair (parallels SetMasterDataById).
    /// </summary>
    void SetUnitById(LookupDto<Guid> lookup);

    /// <summary>
    /// Tries to fetch a previously-cached unit lookup by id without making an API call.
    /// </summary>
    LookupDto<Guid>? TryGetUnitById(Guid id);
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

    public async Task<LookupDto<Guid>?> GetMasterDataByIdAsync(Guid id, Func<Task<MasterDataDto>> loadFactory)
    {
        var key = CacheKey($"mdi-{id}");
        if (_memoryCache.TryGetValue(key, out LookupDto<Guid>? cached))
        {
            return cached;
        }

        try
        {
            var dto = await loadFactory();
            var lookup = new LookupDto<Guid> { Id = dto.Id, DisplayName = dto.Name };
            _memoryCache.Set(key, lookup, new MemoryCacheEntryOptions { SlidingExpiration = CacheDuration });
            return lookup;
        }
        catch (EntityNotFoundException)
        {
            return null;
        }
        catch (AbpRemoteCallException ex) when (ex.HttpStatusCode == (int)System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public void SetMasterDataById(LookupDto<Guid> lookup)
    {
        if (lookup == null || lookup.Id == Guid.Empty)
        {
            return;
        }

        var key = CacheKey($"mdi-{lookup.Id}");
        _memoryCache.Set(key, lookup, new MemoryCacheEntryOptions { SlidingExpiration = CacheDuration });
    }

    public void SetUnitById(LookupDto<Guid> lookup)
    {
        if (lookup == null || lookup.Id == Guid.Empty)
        {
            return;
        }

        var key = CacheKey($"unit-{lookup.Id}");
        _memoryCache.Set(key, lookup, new MemoryCacheEntryOptions { SlidingExpiration = CacheDuration });
    }

    public LookupDto<Guid>? TryGetUnitById(Guid id)
    {
        if (id == Guid.Empty)
        {
            return null;
        }

        var key = CacheKey($"unit-{id}");
        return _memoryCache.TryGetValue(key, out LookupDto<Guid>? cached) ? cached : null;
    }
}
