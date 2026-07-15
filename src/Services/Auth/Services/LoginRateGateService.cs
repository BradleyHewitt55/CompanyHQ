using System.Collections.Concurrent;

namespace Services.Auth.Services;

public sealed class LoginRateGateService
{
    private readonly ConcurrentDictionary<string, (int Count, DateTimeOffset Until)> attempts = new();
    public bool IsLimited(string key, int limit) => attempts.TryGetValue(key, out var value) && value.Until > DateTimeOffset.UtcNow && value.Count >= limit;
    public int Fail(string key) => attempts.AddOrUpdate(key, _ => (1, DateTimeOffset.UtcNow.AddMinutes(1)), (_, value) => value.Until <= DateTimeOffset.UtcNow ? (1, DateTimeOffset.UtcNow.AddMinutes(1)) : (value.Count + 1, value.Until)).Count;
    public void Succeed(string key) => attempts.TryRemove(key, out _);
}
