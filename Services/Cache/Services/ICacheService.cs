using CompanyHQ.Services.Cache.Contracts.Models;

namespace CompanyHQ.Services.Cache.Services;

/// <summary>
/// The only way CompanyHQ reads or writes the shared cache. Values are stored as JSON, keys are
/// namespaced by <c>Cache:KeyPrefix</c>, and every entry carries a TTL.
/// </summary>
public interface ICacheService
{
    /// <summary>Returns the cached value, or <c>default</c> when the key is absent or unreadable.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Writes a value, falling back to <c>Cache:DefaultTtlSeconds</c> when no TTL is given.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan? ttl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the cached value, or invokes <paramref name="factory"/> and caches its result.
    /// Concurrent callers may both run the factory, so it must be safe to run twice.
    /// </summary>
    Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default);

    /// <summary>Removes a key. <c>false</c> when nothing was there.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes every key under a prefix — the invalidation path for grouped entries such as
    /// <c>order:{id}:</c>. Scans rather than blocking the server on <c>KEYS</c>.
    /// </summary>
    Task<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically increments a short-lived counter. An unavailable counter is reported instead of
    /// being swallowed so callers using it for security decisions can fail closed.
    /// </summary>
    Task<CacheCounterResult> IncrementAsync(
        string key,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}
