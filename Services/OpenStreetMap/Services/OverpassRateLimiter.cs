using CompanyHQ.Services.OpenStreetMap.Contracts.Models;

namespace CompanyHQ.Services.OpenStreetMap.Services;

/// <summary>Serialises Overpass calls so the shared public endpoint is not burst by concurrent maps.</summary>
public sealed class OverpassRateLimiter(OpenStreetMapOptions options, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private long lastRequestTimestamp;

    public async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> send,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(send);

        await gate.WaitAsync(cancellationToken);

        try
        {
            var wait = options.OverpassMinimumRequestInterval - time.GetElapsedTime(lastRequestTimestamp);
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
