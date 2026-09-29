using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompanyHQ.Services.Notifications.Services;

/// <summary>Drains the notification queue by running one <see cref="NotificationQueueProcessor"/> batch per scope.</summary>
public sealed class NotificationWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<NotificationWorker> logger) : BackgroundService
{
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<NotificationQueueProcessor>();

                if (await processor.ProcessBatchAsync(stoppingToken) == 0)
                {
                    await Task.Delay(EmptyQueueDelay, timeProvider, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                // Provider and data exceptions can carry request details. Keep the queue log
                // useful without allowing a token, recipient, or message body into it.
                logger.LogError(
                    "Notification queue processing failed with {ExceptionType}.",
                    exception.GetType().Name);
                await Task.Delay(EmptyQueueDelay, timeProvider, stoppingToken);
            }
        }
    }
}
