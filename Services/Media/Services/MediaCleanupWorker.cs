using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CompanyHQ.Media.Contracts.Models;

namespace CompanyHQ.Media.Services;

/// <summary>Runs one <see cref="MediaCleanupProcessor"/> pass per scope, every cleanup interval.</summary>
public sealed class MediaCleanupWorker(
    IServiceScopeFactory scopeFactory,
    MediaOptions options,
    TimeProvider clock,
    ILogger<MediaCleanupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<MediaCleanupProcessor>().CleanupBatchAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Media cleanup pass failed; the next pass will retry.");
            }

            try
            {
                await Task.Delay(options.CleanupInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        }
    }
}
