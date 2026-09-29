using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Auth.Contracts.Models;

namespace CompanyHQ.Services.Auth.Services;

/// <summary>Runs one <see cref="RefreshTokenCleanupProcessor"/> batch per scope on every interval tick.</summary>
public sealed class RefreshTokenCleanupWorker(
    IServiceScopeFactory scopeFactory,
    RefreshTokenCleanupOptions options,
    TimeProvider clock,
    ILogger<RefreshTokenCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Interval, clock);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<RefreshTokenCleanupProcessor>()
                    .CleanupBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Maintenance must never take the auth host down. The next bounded tick retries it.
                logger.LogWarning(exception, "Refresh-token cleanup batch failed.");
            }
        }
    }
}
