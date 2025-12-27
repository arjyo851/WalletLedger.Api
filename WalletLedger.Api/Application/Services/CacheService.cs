using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using WalletLedger.Api.Application.Interfaces;

namespace WalletLedger.Api.Application.Services;

public class CacheService : ICacheService
{
    private readonly IDistributedCache _cache;
    private readonly ILogger<CacheService> _logger;

    public CacheService(IDistributedCache cache, ILogger<CacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key)
    {
        try
        {
            var cachedValue = await _cache.GetStringAsync(key);
            if (string.IsNullOrEmpty(cachedValue))
                return default(T);

            return JsonSerializer.Deserialize<T>(cachedValue);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to get value from cache for key: {Key}", key);
            return default(T);
        }
    }

    public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();
            if (expiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = expiration;
            }
            else
            {
                // Default expiration: 5 minutes
                options.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            }

            var serializedValue = JsonSerializer.Serialize(value);
            await _cache.SetStringAsync(key, serializedValue, options);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to set value in cache for key: {Key}", key);
        }
    }

    public async Task RemoveAsync(string key)
    {
        try
        {
            await _cache.RemoveAsync(key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to remove value from cache for key: {Key}", key);
        }
    }

    public async Task RemoveByPatternAsync(string pattern)
    {
        // Note: IDistributedCache doesn't support pattern-based deletion
        // This would require Redis-specific implementation or a wrapper
        // For now, we'll log a warning
        _logger.LogWarning("Pattern-based cache removal is not fully supported with IDistributedCache. Pattern: {Pattern}", pattern);
        await Task.CompletedTask;
    }
}

