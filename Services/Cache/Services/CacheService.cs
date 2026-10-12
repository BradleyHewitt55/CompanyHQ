using System.Text.Json;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Cache.Contracts.Models;
using CompanyHQ.Services.Cache.Contracts.Ports;

namespace CompanyHQ.Services.Cache.Services;

/// <summary>
/// <see cref="ICacheService"/> over whichever <see cref="ICacheProvider"/> the host composes. Every
/// provider failure is swallowed and reported as a miss when <c>Cache:FailSilently</c> is on, so a
/// cache outage degrades reads instead of surfacing as a 500.
/// </summary>
public sealed class CacheService(
    ICacheProvider provider,
    CacheOptions options,
    CacheSerializer serializer,
    ILogger<CacheService> logger) : ICacheService
{
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        var (_, value) = await TryGetAsync<T>(key, cancellationToken);

        return value;
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            await provider.SetAsync(Qualify(key), serializer.Serialize(value), ttl ?? options.DefaultTtl, cancellationToken);
        }
        catch (Exception exception) when (ShouldSwallow(exception))
        {
            logger.LogWarning(exception, "Cache write failed for {Key}; the value was not cached.", key);
        }
    }

    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<CancellationToken, Task<T>> factory,
        TimeSpan? ttl = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(factory);

        // Hit/miss is tracked separately from the value: a cached `0` or `false` is a hit, and
        // testing the value for null would re-run the factory on every read.
        var (found, cached) = await TryGetAsync<T>(key, cancellationToken);
        if (found)
        {
            return cached!;
        }

        var value = await factory(cancellationToken);
        await SetAsync(key, value, ttl, cancellationToken);

        return value;
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await provider.RemoveAsync(Qualify(key), cancellationToken);
        }
        catch (Exception exception) when (ShouldSwallow(exception))
        {
            logger.LogWarning(exception, "Cache eviction failed for {Key}.", key);
            return false;
        }
    }

    public async Task<long> RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        var qualified = Qualify(prefix);
        var removed = 0L;

        try
        {
            // Counted batch by batch, so a failure part way still reports what was already removed.
            await foreach (var batch in provider.RemoveByPrefixAsync(qualified, options.ScanPageSize, cancellationToken))
            {
                removed += batch;
            }
        }
        catch (Exception exception) when (ShouldSwallow(exception))
        {
            logger.LogWarning(exception, "Cache eviction failed for prefix {Prefix}.", prefix);
        }

        return removed;
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            return await provider.ExistsAsync(Qualify(key), cancellationToken);
        }
        catch (Exception exception) when (ShouldSwallow(exception))
        {
            logger.LogWarning(exception, "Cache probe failed for {Key}.", key);
            return false;
        }
    }

    public async Task<CacheCounterResult> IncrementAsync(
        string key,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(window, TimeSpan.Zero);
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var counter = await provider.IncrementAsync(Qualify(key), window, cancellationToken);

            return new CacheCounterResult(
                IsAvailable: true,
                Count: counter.Count,
                // A provider reports a missing or already-lapsed window as zero or less.
                RetryAfter: TimeSpan.FromSeconds(Math.Max(1, (long)counter.TimeToLive.TotalSeconds)));
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // Security callers must not mistake a cache miss for permission to continue. They
            // decide how to fail closed, while ordinary cache reads retain their fail-silent policy.
            logger.LogWarning(exception, "Atomic cache counter failed for {Key}.", key);
            return CacheCounterResult.Unavailable;
        }
    }

    private async Task<(bool Found, T? Value)> TryGetAsync<T>(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var payload = await provider.GetAsync(Qualify(key), cancellationToken);

            return string.IsNullOrEmpty(payload) ? (false, default) : (true, serializer.Deserialize<T>(payload));
        }
        catch (Exception exception) when (ShouldSwallow(exception))
        {
            logger.LogWarning(exception, "Cache read failed for {Key}; treating as a miss.", key);
            return (false, default);
        }
    }

    private string Qualify(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return options.KeyPrefix + key;
    }

    // An outage is the provider's to name; an unreadable payload or a timeout is one whoever threw it.
    private bool ShouldSwallow(Exception exception) =>
        options.FailSilently
        && (exception is JsonException or TimeoutException || provider.IsTransportFailure(exception));
}
