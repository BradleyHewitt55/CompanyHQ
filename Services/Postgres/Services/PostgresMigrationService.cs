using System.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using CompanyHQ.Postgres.Contracts.Models;
using CompanyHQ.Postgres.Data;

namespace CompanyHQ.Postgres.Services;

/// <summary>
/// Serialises database startup, applies migrations, and then runs seeders before the host accepts
/// traffic.
/// </summary>
/// <remarks>
/// The PostgreSQL advisory lock spans both migration and seeding, so two API replicas cannot observe
/// a half-created schema or race an idempotent seeder. Consumer hosts disable both operations and
/// never participate in startup writes.
/// </remarks>
public sealed class PostgresMigrationService(
    IServiceProvider services,
    PostgresOptions options,
    ILogger<PostgresMigrationService> logger) : IHostedService
{
    // A fixed application-level key is enough: every CompanyHQ database must have one startup gate,
    // and the lock is scoped to the current PostgreSQL database.
    private const long StartupLockKey = 7_894_632_105_781;

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!options.AutoMigrate && !options.AutoSeed)
        {
            logger.LogInformation("Postgres startup migration and seeding are disabled; skipping.");
            return;
        }

        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<PostgresDbContext>();

        await context.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            await AcquireStartupLockAsync(context, cancellationToken);

            if (options.AutoMigrate)
            {
                await MigrateAsync(context, cancellationToken);
            }
            else
            {
                logger.LogInformation("Postgres auto-migration is disabled; assuming the schema is managed externally.");
            }

            if (options.AutoSeed)
            {
                await SeedAsync(scope.ServiceProvider, context, cancellationToken);
            }
            else
            {
                logger.LogInformation("Postgres auto-seeding is disabled for this host.");
            }
        }
        finally
        {
            // Closing the session also releases the advisory lock. Explicitly unlocking makes the
            // intent clear and avoids holding it if the connection pool ever changes behaviour.
            await ReleaseStartupLockAsync(context);
            await context.Database.CloseConnectionAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task MigrateAsync(PostgresDbContext context, CancellationToken cancellationToken)
    {
        var pending = (await context.Database.GetPendingMigrationsAsync(cancellationToken)).ToArray();
        if (pending.Length == 0)
        {
            logger.LogInformation("Postgres schema is up to date.");
            return;
        }

        logger.LogInformation(
            "Applying {Count} pending Postgres migration(s): {Migrations}",
            pending.Length,
            string.Join(", ", pending));

        await context.Database.MigrateAsync(cancellationToken);
        logger.LogInformation("Postgres migrations applied.");
    }

    private async Task SeedAsync(
        IServiceProvider provider,
        PostgresDbContext context,
        CancellationToken cancellationToken)
    {
        // Provider-neutral seeders and this provider's own run as one sequence, so Order means the
        // same thing whichever port a seeder was written against. Both write through this context.
        var persistence = provider.GetRequiredService<CompanyHQ.Persistence.Services.IPersistence>();
        var seeders = provider.GetServices<CompanyHQ.Persistence.Services.IDataSeeder>()
            .Select(seeder => (seeder.Order, Name: seeder.GetType().Name,
                Run: (Func<Task>)(() => seeder.SeedAsync(persistence, cancellationToken))))
            .Concat(provider.GetServices<IDataSeeder>()
                .Select(seeder => (seeder.Order, Name: seeder.GetType().Name,
                    Run: (Func<Task>)(() => seeder.SeedAsync(context, cancellationToken)))))
            .OrderBy(seeder => seeder.Order)
            .ToArray();

        foreach (var seeder in seeders)
        {
            logger.LogInformation("Running seeder {Seeder}.", seeder.Name);
            await seeder.Run();
            // A seeder must save what it stages. Clearing its now-clean entries prevents a later
            // seeder from accidentally committing state it did not create.
            context.ChangeTracker.Clear();
        }
    }

    private static async Task AcquireStartupLockAsync(
        PostgresDbContext context,
        CancellationToken cancellationToken)
    {
        await using var command = context.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT pg_advisory_lock(@startup_lock_key);";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "startup_lock_key";
        parameter.DbType = DbType.Int64;
        parameter.Value = StartupLockKey;
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task ReleaseStartupLockAsync(PostgresDbContext context)
    {
        try
        {
            await using var command = context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "SELECT pg_advisory_unlock(@startup_lock_key);";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "startup_lock_key";
            parameter.DbType = DbType.Int64;
            parameter.Value = StartupLockKey;
            command.Parameters.Add(parameter);
            await command.ExecuteScalarAsync();
        }
        catch (Exception)
        {
            // Closing the open connection below releases a session-level lock even if the explicit
            // unlock cannot complete during shutdown/cancellation.
        }
    }
}
