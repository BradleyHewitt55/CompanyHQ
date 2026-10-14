using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompanyHQ.Email.Services;

/// <summary>
/// Drains <c>queued_email</c> by running one <see cref="EmailQueueProcessor"/> batch per scope.
/// Several API instances can run it: a claim is row-locked and leased, never shared.
/// </summary>
public sealed class EmailWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider timeProvider,
    ILogger<EmailWorker> logger) : BackgroundService
{
    private static readonly TimeSpan EmptyQueueDelay = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<EmailQueueProcessor>();

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
                // Exceptions can carry recipients or bodies; the type name is all the log gets.
                logger.LogError("Email queue processing failed with {ExceptionType}.", exception.GetType().Name);
                await Task.Delay(EmptyQueueDelay, timeProvider, stoppingToken);
            }
        }
    }
}
