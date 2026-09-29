using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Postgres.Services;

/// <summary>
/// Runs every registered <see cref="IDataSeeder"/> once the schema is up to date.
/// </summary>
/// <remarks>
/// Registered after <see cref="PostgresMigrationService"/> because hosted services start in
/// registration order, so seeders never run against a stale schema.
/// </remarks>
public sealed class PostgresSeedService(
    IServiceProvider services,
    ILogger<PostgresSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();

        var seeders = scope.ServiceProvider.GetServices<IDataSeeder>()
            .OrderBy(seeder => seeder.Order)
            .ToArray();

        if (seeders.Length == 0)
        {
            return;
        }

        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        foreach (var seeder in seeders)
        {
            logger.LogInformation("Running seeder {Seeder}.", seeder.GetType().Name);
            await seeder.SeedAsync(context, cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
