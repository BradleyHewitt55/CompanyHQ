using Abstractions.Companies;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CompanyHQ.Services.Postgress.Services;

// Runs before the host serves traffic: syncs the platform database (if configured)
// and every registered company's database to the loaded plugin model.
public sealed class SchemaMigrator(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<SchemaMigrator> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetConnectionString("CompanyHq") is not null)
        {
            await SyncOneAsync(company: null, cancellationToken);
        }

        // Registry is optional: without the Companies plugin this provider still
        // serves the single platform database, as before per-company hosting existed.
        var registry = serviceProvider.GetService<ICompanyRegistry>();
        if (registry is null)
        {
            return;
        }

        var databases = serviceProvider.GetRequiredService<CompanyDatabases>();
        foreach (var company in await registry.ListAsync(cancellationToken))
        {
            // Registered but unconfigured companies are waiting on the user's
            // hosted-vs-external choice in the dashboard gate; nothing to sync yet.
            if (!databases.IsConfigured(company))
            {
                continue;
            }

            try
            {
                await SyncOneAsync(company, cancellationToken);
            }
            catch (Exception exception)
            {
                // One company's broken database (bad external string, server down)
                // must not keep every other company from starting.
                logger.LogError(exception, "Schema sync failed for company {Company}.", company);
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SyncOneAsync(string? company, CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        if (company is not null)
        {
            scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = company;
        }

        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        await scope.ServiceProvider.GetRequiredService<SchemaSynchronizer>()
            .SyncAsync(context, cancellationToken);
    }
}
