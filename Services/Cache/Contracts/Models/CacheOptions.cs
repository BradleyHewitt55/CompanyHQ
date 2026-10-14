namespace CompanyHQ.Cache.Contracts.Models;

/// <summary>
/// Configuration for the shared CompanyHQ cache, bound from the <see cref="SectionName"/>
/// configuration section.
/// </summary>
public sealed class CacheOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// Where the cache lives, as a StackExchange.Redis configuration string, e.g.
    /// <c>redis:6379,password=…,defaultDatabase=0</c>. The host builds its
    /// <c>IConnectionMultiplexer</c> from it; Cache only requires that it is set.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Prepended to every key. Keeps CompanyHQ keys distinguishable when a cache instance is
    /// shared, and gives <c>RemoveByPrefixAsync</c> a bounded namespace to scan.
    /// </summary>
    public string KeyPrefix { get; set; } = "companyhq:";

    /// <summary>Expiry applied when a caller does not pass one. Never write entries without a TTL.</summary>
    public int DefaultTtlSeconds { get; set; } = 300;

    /// <summary>
    /// Keys scanned per round trip by <c>RemoveByPrefixAsync</c>. Small pages keep a large
    /// keyspace from blocking the server on one call.
    /// </summary>
    public int ScanPageSize { get; set; } = 250;

    /// <summary>
    /// Treats a cache miss as the failure mode for every provider error instead of throwing. A cache
    /// is an optimisation; an unreachable one should degrade the app, not break it.
    /// </summary>
    public bool FailSilently { get; set; } = true;

    public TimeSpan DefaultTtl => TimeSpan.FromSeconds(DefaultTtlSeconds);

    /// <summary>Throws when the bound configuration cannot produce a usable cache.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{SectionName}:ConnectionString is required.");
        }

        if (DefaultTtlSeconds <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:DefaultTtlSeconds must be greater than zero.");
        }

        if (ScanPageSize <= 0)
        {
            throw new InvalidOperationException($"{SectionName}:ScanPageSize must be greater than zero.");
        }
    }
}
