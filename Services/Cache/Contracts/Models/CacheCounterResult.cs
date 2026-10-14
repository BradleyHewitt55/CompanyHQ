namespace CompanyHQ.Cache.Contracts.Models;

/// <summary>The result of an atomic distributed counter increment.</summary>
public readonly record struct CacheCounterResult(
    bool IsAvailable,
    long Count,
    TimeSpan RetryAfter)
{
    public static CacheCounterResult Unavailable => new(false, 0, TimeSpan.Zero);
}
