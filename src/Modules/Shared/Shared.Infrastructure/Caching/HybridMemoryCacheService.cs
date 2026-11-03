using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace AgentMemoryService.Shared.Infrastructure.Caching;

// TODO: swap IMemoryCache for HybridCache when Redis is available
public sealed class HybridMemoryCacheService : IMemoryCacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private static readonly TimeSpan DefaultExpiry = TimeSpan.FromMinutes(5);

    public HybridMemoryCacheService(IMemoryCache cache) => _cache = cache;

    public async Task<T?> GetOrCreateAsync<T>(string key, Func<CancellationToken, Task<T>> factory,
        TimeSpan? expiry = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cached))
            return cached;

        var value = await factory(ct);

        var options = new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = expiry ?? DefaultExpiry
        };
        _cache.Set(key, value, options);
        _keys.TryAdd(key, 0);

        return value;
    }

    public Task RemoveAsync(string key, CancellationToken ct = default)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct = default)
    {
        foreach (var key in _keys.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }
        return Task.CompletedTask;
    }
}
