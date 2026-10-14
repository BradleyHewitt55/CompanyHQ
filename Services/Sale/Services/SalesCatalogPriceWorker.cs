using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompanyHQ.Services.Sales.Services;

/// <summary>Warms date/policy-invalidated projections in small, resumable transactions.</summary>
internal sealed class SalesCatalogPriceWorker(IServiceScopeFactory scopes, ILogger<SalesCatalogPriceWorker> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                bool more;
                do
                {
                    await using var scope = scopes.CreateAsyncScope();
                    var catalog = scope.ServiceProvider.GetRequiredService<SalesCatalogService>();
                    await catalog.EnsurePriceStateAsync(stoppingToken);
                    more = await catalog.RefreshPriceBatchAsync(stoppingToken);
                } while (more && !stoppingToken.IsCancellationRequested);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception error) { log.LogError(error, "Catalog price projection batch failed; it will retry."); }
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
