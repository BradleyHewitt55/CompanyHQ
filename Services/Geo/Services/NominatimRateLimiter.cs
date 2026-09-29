using CompanyHQ.Services.Geo.Contracts.Models;

namespace CompanyHQ.Services.Geo.Services;

/// <summary>
/// Serialises every outbound Nominatim call and spaces the calls by
/// <see cref="GeoOptions.MinimumRequestInterval"/>.
/// </summary>
/// <remarks>
/// Registered as a singleton because the limit is per client IP, not per request scope: a
/// per-instance gate would let concurrent requests burst straight past it and get the deployment
/// banned from the public instance.
/// </remarks>
public sealed class NominatimRateLimiter(GeoOptions options, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private long lastRequestTimestamp;

    /// <summary>Runs <paramref name="send"/> once the interval since the previous call has elapsed.</summary>
    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);

        await gate.WaitAsync(cancellationToken);

        try
        {
            // lastRequestTimestamp is 0 on the first call, which GetElapsedTime reports as the
            // whole process uptime — comfortably past the interval, so the first call never waits.
            var wait = options.MinimumRequestInterval - time.GetElapsedTime(lastRequestTimestamp);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, cancellationToken);
            }

            return await send(cancellationToken);
        }
        finally
        {
            lastRequestTimestamp = time.GetTimestamp();
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
