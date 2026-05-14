using System;
using System.Threading.Tasks;
using Volo.Abp.Authorization;
using Volo.Abp.Caching;

namespace HC;

/// <summary>
/// Validates short-lived Excel export tokens issued while the user is authenticated,
/// then removes the token from distributed cache so it cannot be replayed on anonymous endpoints.
/// </summary>
public static class ExcelDownloadAnonymousTokenHelper
{
    public static async Task ValidateAndConsumeOneTimeExportTokenAsync<TCacheItem>(
        IDistributedCache<TCacheItem, string> cache,
        string inputToken,
        Func<TCacheItem, string> readTokenFromCacheItem)
        where TCacheItem : class
    {
        if (string.IsNullOrEmpty(inputToken))
        {
            throw new AbpAuthorizationException("Invalid download token.");
        }

        var cacheItem = await cache.GetAsync(inputToken);
        if (cacheItem == null || readTokenFromCacheItem(cacheItem) != inputToken)
        {
            throw new AbpAuthorizationException("Invalid download token.");
        }

        await cache.RemoveAsync(inputToken);
    }
}
