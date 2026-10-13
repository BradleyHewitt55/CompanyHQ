namespace CompanyHQ.Services.Cache.Contracts.Ports;

/// <summary>
/// The transport Cache stores its entries through. Cache fills it itself with the host's Redis
/// connection; it stays a port so the cache policy can be exercised without Redis.
/// </summary>
/// <remarks>
/// Every key and prefix arrives fully qualified and every payload already serialised, so a provider
/// never adds a namespace, changes a payload or chooses a TTL. It throws its own transport
/// exceptions unchanged and names them through <see cref="IsTransportFailure"/>; Cache decides
/// whether one becomes a miss.
/// </remarks>
public interface ICacheProvider
{
    /// <summary>The technology behind this provider in lower case, e.g. <c>redis</c>. Tags the <c>cache</c> health entry.</summary>
    string Name { get; }

    /// <summary>The stored payload, or <c>null</c> when the key is absent.</summary>
    Task<string?> GetAsync(string key, CancellationToken cancellationToken);

    /// <summary>Stores a payload that expires after <paramref name="ttl"/>. There is no entry without one.</summary>
    Task SetAsync(string key, string payload, TimeSpan ttl, CancellationToken cancellationToken);

    /// <summary>Removes one key. <c>false</c> when nothing was there.</summary>
    Task<bool> RemoveAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Removes every key starting with <paramref name="prefix"/>, <paramref name="pageSize"/> keys
    /// at a time, yielding how many each batch removed. It is a stream so the caller keeps the
    /// running total when the transport fails part way, and can cancel between pages.
    /// </summary>
    IAsyncEnumerable<long> RemoveByPrefixAsync(string prefix, int pageSize, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(string key, CancellationToken cancellationToken);

    /// <summary>
    /// Atomically adds one to a counter and starts its <paramref name="window"/> on the first
    /// increment only, so concurrent callers can never extend it.
    /// </summary>
    Task<CacheProviderCounter> IncrementAsync(string key, TimeSpan window, CancellationToken cancellationToken);

    /// <summary>Round-trips the provider and returns the latency, for the <c>cache</c> health entry.</summary>
    Task<TimeSpan> PingAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Whether <paramref name="exception"/> is this provider's own transport failure: an outage,
    /// not a caller bug. Cache reports those as misses while <c>Cache:FailSilently</c> is on.
    /// </summary>
    bool IsTransportFailure(Exception exception);
}

/// <summary>A counter's value after an increment, and how long its window has left as the provider reports it.</summary>
public readonly record struct CacheProviderCounter(long Count, TimeSpan TimeToLive);
