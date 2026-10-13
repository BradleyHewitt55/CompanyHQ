using System.Runtime.CompilerServices;
using CompanyHQ.Services.Cache.Contracts.Ports;
using StackExchange.Redis;

namespace CompanyHQ.Services.Cache.Services;

/// <summary>
/// <see cref="ICacheProvider"/> over the host's <see cref="IConnectionMultiplexer"/>. It carries
/// keys, payloads and expiries exactly as handed over; what they mean is <see cref="CacheService"/>'s
/// business.
/// </summary>
internal sealed class RedisCacheProvider(IConnectionMultiplexer connection) : ICacheProvider
{
    private const string IncrementScript = """
        local count = redis.call('INCR', KEYS[1])
        if count == 1 then
            redis.call('EXPIRE', KEYS[1], ARGV[1])
        end
        local ttl = redis.call('TTL', KEYS[1])
        return { count, ttl }
        """;

    public string Name => "redis";

    private IDatabase Database => connection.GetDatabase();

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken) =>
        await Database.StringGetAsync(key);

    public Task SetAsync(string key, string payload, TimeSpan ttl, CancellationToken cancellationToken) =>
        Database.StringSetAsync(key, payload, ttl);

    public Task<bool> RemoveAsync(string key, CancellationToken cancellationToken) =>
        Database.KeyDeleteAsync(key);

    public async IAsyncEnumerable<long> RemoveByPrefixAsync(
        string prefix,
        int pageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // A glob, and deliberately unescaped: the prefix is matched as Redis matches a pattern.
        var pattern = $"{prefix}*";

        foreach (var endpoint in connection.GetEndPoints())
        {
            var server = connection.GetServer(endpoint);

            // Replicas mirror the primary, so deleting through them is both refused and pointless.
            if (server.IsReplica || !server.IsConnected)
            {
                continue;
            }

            var batch = new List<RedisKey>(pageSize);

            await foreach (var key in server
                .KeysAsync(pattern: pattern, pageSize: pageSize)
                .WithCancellation(cancellationToken))
            {
                batch.Add(key);

                if (batch.Count < pageSize)
                {
                    continue;
                }

                yield return await Database.KeyDeleteAsync([.. batch]);
                batch.Clear();
            }

            if (batch.Count > 0)
            {
                yield return await Database.KeyDeleteAsync([.. batch]);
            }
        }
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken) =>
        Database.KeyExistsAsync(key);

    public async Task<CacheProviderCounter> IncrementAsync(
        string key,
        TimeSpan window,
        CancellationToken cancellationToken)
    {
        // EXPIRE counts whole seconds, so a shorter window still lasts one.
        var result = await Database.ScriptEvaluateAsync(
            IncrementScript,
            [key],
            [Math.Max(1, (long)Math.Ceiling(window.TotalSeconds))]);
        var values = (RedisResult[])result!;

        return new CacheProviderCounter((long)values[0], TimeSpan.FromSeconds((long)values[1]));
    }

    public Task<TimeSpan> PingAsync(CancellationToken cancellationToken) => Database.PingAsync();

    public bool IsTransportFailure(Exception exception) => exception is RedisException;
}
