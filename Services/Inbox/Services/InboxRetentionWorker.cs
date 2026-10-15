using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompanyHQ.Inbox.Services;

/// <summary>Deletes expired resolved conversations once at startup and then hourly.</summary>
internal sealed class InboxRetentionWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    ILogger<InboxRetentionWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1), clock);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var availability = scope.ServiceProvider.GetService<IServiceAvailability>();
                if (availability is not null && (await availability.GetAsync("inbox", stoppingToken))?.Status != ServiceStatus.EnabledReady) continue;
                await scope.ServiceProvider.GetRequiredService<IVisitorConversationService>()
                    .PurgeExpiredAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (ServiceUnavailableException) { /* Switch changed between admission and purge. */ }
            catch (Exception exception)
            {
                // Maintenance must never take the host down. The next tick retries it.
                logger.LogWarning(exception, "Inbox conversation retention purge failed.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
