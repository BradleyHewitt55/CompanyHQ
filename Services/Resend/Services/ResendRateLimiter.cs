using CompanyHQ.Services.Resend.Contracts.Models;

namespace CompanyHQ.Services.Resend.Services;

/// <summary>
/// Spaces every Resend request, retries included, at least <c>1 / MaxRequestsPerSecond</c> apart.
/// Ordering and priority belong to the Email outbox; this only paces whatever it is handed.
/// </summary>
/// <remarks>
/// A singleton because the provider is a transient typed client: a per-instance gate would let
/// concurrent sends burst straight past it.
/// </remarks>
// ponytail: per process. Two API replicas send at twice the rate; move to a Redis-backed limiter
// through Services/Cache if the API scales out.
public sealed class ResendRateLimiter(ResendOptions options, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan interval = TimeSpan.FromSeconds(1.0 / options.MaxRequestsPerSecond);
    private long lastRequestTimestamp;

    /// <summary>Completes once the interval since the previous request has elapsed.</summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            // 0 on the first call reads as the whole process uptime, so the first send never waits.
            var wait = interval - time.GetElapsedTime(lastRequestTimestamp);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, cancellationToken);
            }

            lastRequestTimestamp = time.GetTimestamp();
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
