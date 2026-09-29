using System.Text.Json;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Cache.Contracts.Models;
using StackExchange.Redis;

namespace CompanyHQ.Services.Cache.Services;

/// <summary>
/// Redis-backed <see cref="ICacheService"/>. Every Redis failure is swallowed and reported as a
/// miss when <c>Cache:FailSilently</c> is on, so a cache outage degrades reads instead of
/// surfacing as a 500.
/// </summary>
public sealed class CacheService(
    IConnectionMultiplexer connection,
    CacheOptions options,
    CacheSerializer serializer,
    ILogger<CacheService> logger) : ICacheService
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('TTL', KEYS[1])
        return { count, ttl }
        """;

    private IDatabase Database => connection.GetDatabase();

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
            await Database.StringSetAsync(Qualify(key), serializer.Serialize(value), ttl ?? options.DefaultTtl);
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
            return await Database.KeyDeleteAsync(Qualify(key));
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

        var pattern = $"{Qualify(prefix)}*";
        var removed = 0L;

        try
        {
            foreach (var endpoint in connection.GetEndPoints())
            {
                var server = connection.GetServer(endpoint);

                // Replicas mirror the primary, so deleting through them is both refused and pointless.
                if (server.IsReplica || !server.IsConnected)
                {
                    continue;
                }

                var batch = new List<RedisKey>(options.ScanPageSize);

                await foreach (var key in server
                    .KeysAsync(pattern: pattern, pageSize: options.ScanPageSize)
                    .WithCancellation(cancellationToken))
                {
                    batch.Add(key);

                    if (batch.Count < options.ScanPageSize)
                    {
                        continue;
                    }

                    removed += await Database.KeyDeleteAsync([.. batch]);
                    batch.Clear();
                }

                if (batch.Count > 0)
                {
                    removed += await Database.KeyDeleteAsync([.. batch]);
                }
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
            return await Database.KeyExistsAsync(Qualify(key));
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
            var result = await Database.ScriptEvaluateAsync(
                IncrementScript,
                [Qualify(key)],
                [Math.Max(1, (long)Math.Ceiling(window.TotalSeconds))]);
            var values = (RedisResult[])result!;
            var count = (long)values[0];
            var ttlSeconds = Math.Max(1, (long)values[1]);

            return new CacheCounterResult(
                IsAvailable: true,
                Count: count,
                RetryAfter: TimeSpan.FromSeconds(ttlSeconds));
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
            var value = await Database.StringGetAsync(Qualify(key));

            return value.IsNullOrEmpty ? (false, default) : (true, serializer.Deserialize<T>(value!));
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

    private bool ShouldSwallow(Exception exception) =>
        options.FailSilently && exception is RedisException or JsonException or TimeoutException;
}
