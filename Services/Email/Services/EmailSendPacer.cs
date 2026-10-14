using CompanyHQ.Email.Contracts.Models;

namespace CompanyHQ.Email.Services;

/// <summary>
/// Spaces every send at least <c>1 / MaxSendsPerSecond</c> apart. Ordering and priority belong to
/// the outbox; this only paces whatever it is handed.
/// </summary>
/// <remarks>
/// A singleton because the provider is resolved per batch: a per-instance gate would let
/// concurrent batches burst straight past it.
/// </remarks>
// ponytail: per process. Two API replicas send at twice the rate; move to a shared limiter if the
// API scales out.
internal sealed class EmailSendPacer(EmailOptions options, TimeProvider time) : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly TimeSpan interval = TimeSpan.FromSeconds(1.0 / options.MaxSendsPerSecond);
    private long lastSendTimestamp;

    /// <summary>Completes once the interval since the previous send has elapsed.</summary>
    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await gate.WaitAsync(cancellationToken);
        try
        {
            // 0 on the first call reads as the whole process uptime, so the first send never waits.
            var wait = interval - time.GetElapsedTime(lastSendTimestamp);
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, time, cancellationToken);
            }

            lastSendTimestamp = time.GetTimestamp();
        }
        finally
        {
            gate.Release();
        }
    }

    public void Dispose() => gate.Dispose();
}
