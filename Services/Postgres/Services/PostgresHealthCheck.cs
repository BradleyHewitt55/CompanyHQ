using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace CompanyHQ.Postgres.Services;

/// <summary>
/// Verifies the shared data source can open a connection and round-trip a statement.
/// </summary>
public sealed class PostgresHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public const string Name = "postgres";

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var command = dataSource.CreateCommand("SELECT 1;");
            await command.ExecuteScalarAsync(cancellationToken);

            return HealthCheckResult.Healthy();
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return new HealthCheckResult(
                context.Registration.FailureStatus,
                "PostgreSQL is unreachable.",
                exception);
        }
    }
}
