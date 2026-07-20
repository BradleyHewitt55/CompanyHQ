using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.Json;
using Abstractions.Companies;
using Abstractions.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace CompanyHQ.Services.Postgress.Services;

// Each company's credentials live in its own directory (Companies/{Name}/db.json),
// not in central config: the company directory is the unit of ownership and backup.
public sealed record CompanyDatabaseCredentials(string ConnectionString, bool Managed);

public sealed class CompanyDatabases(
    IConfiguration configuration,
    IServiceProvider serviceProvider,
    ILogger<CompanyDatabases> logger) : ICompanyDatabases
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private readonly ConcurrentDictionary<string, string> _cache = new();

    public bool IsConfigured(string companyName) => File.Exists(CredentialsPath(companyName));

    // Resolution order: explicit company -> its db.json; no company -> the platform
    // database. Platform state (users, company index pages) is not tenant data.
    public string Resolve(string? companyName)
    {
        if (companyName is null)
        {
            return configuration.GetConnectionString("CompanyHq")
                ?? throw new InvalidOperationException(
                    "No company selected and no platform 'CompanyHq' connection string is configured.");
        }

        return _cache.GetOrAdd(companyName, name =>
        {
            var path = CredentialsPath(name);
            if (!File.Exists(path))
            {
                throw new InvalidOperationException(
                    $"Company '{name}' has no database credentials at '{path}'. Register the company first.");
            }

            var credentials = JsonSerializer.Deserialize<CompanyDatabaseCredentials>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidOperationException($"Credentials file '{path}' is empty or malformed.");
            return credentials.ConnectionString;
        });
    }

    public async Task EnsureDatabaseAsync(
        string companyName,
        string? externalConnectionString = null,
        CancellationToken cancellationToken = default)
    {
        var path = CredentialsPath(companyName);
        if (!File.Exists(path))
        {
            var credentials = externalConnectionString is not null
                ? new CompanyDatabaseCredentials(externalConnectionString, Managed: false)
                : await ProvisionLocalAsync(companyName, cancellationToken);

            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(credentials, JsonOptions), cancellationToken);
            _cache[companyName] = credentials.ConnectionString;
        }

        // Sync immediately so a freshly registered company is usable without a restart.
        using var scope = serviceProvider.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICompanyContext>().Current = companyName;
        var context = scope.ServiceProvider.GetRequiredService<DbContext>();
        await scope.ServiceProvider.GetRequiredService<SchemaSynchronizer>().SyncAsync(context, cancellationToken);
    }

    // "Host it on this server": create a dedicated role and database on the local
    // Postgres server and generate the credentials the user chose not to provide.
    private async Task<CompanyDatabaseCredentials> ProvisionLocalAsync(
        string companyName, CancellationToken cancellationToken)
    {
        var adminConnectionString = configuration.GetConnectionString("CompanyHqServer")
            ?? throw new InvalidOperationException(
                "Local database hosting requires the 'CompanyHqServer' connection string " +
                "(an admin login on the local Postgres server used to provision company databases).");

        var identifier = Sanitize(companyName);
        var database = $"companyhq_{identifier}";
        var role = $"companyhq_{identifier}";
        var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        await using var connection = new NpgsqlConnection(adminConnectionString);
        await connection.OpenAsync(cancellationToken);

        // Re-provisioning after a lost db.json must not fail on leftovers, so both
        // statements tolerate the object already existing (password gets rotated).
        var roleExists = await ScalarAsync(connection,
            "SELECT 1 FROM pg_roles WHERE rolname = @name", role, cancellationToken);
        await ExecuteAsync(connection, roleExists
            ? $"ALTER ROLE \"{role}\" LOGIN PASSWORD '{password}'"
            : $"CREATE ROLE \"{role}\" LOGIN PASSWORD '{password}'", cancellationToken);

        var databaseExists = await ScalarAsync(connection,
            "SELECT 1 FROM pg_database WHERE datname = @name", database, cancellationToken);
        if (!databaseExists)
        {
            // CREATE DATABASE cannot run in a transaction or use parameters; the
            // identifier is safe because Sanitize reduces it to [a-z0-9_].
            await ExecuteAsync(connection, $"CREATE DATABASE \"{database}\" OWNER \"{role}\"", cancellationToken);
        }

        logger.LogInformation("Provisioned local database {Database} for company {Company}.", database, companyName);

        var builder = new NpgsqlConnectionStringBuilder(adminConnectionString)
        {
            Database = database,
            Username = role,
            Password = password,
        };
        return new CompanyDatabaseCredentials(builder.ConnectionString, Managed: true);
    }

    private string CredentialsPath(string companyName)
    {
        var paths = serviceProvider.GetService<IServerPaths>()
            ?? throw new InvalidOperationException(
                "Per-company databases require the Companies plugin (IServerPaths is not registered).");
        return Path.Combine(paths.CompanyDirectory(companyName), "db.json");
    }

    private static string Sanitize(string companyName)
    {
        var chars = companyName.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_')
            .ToArray();
        return new string(chars);
    }

    private static async Task<bool> ScalarAsync(
        NpgsqlConnection connection, string sql, string name, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("name", name);
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task ExecuteAsync(
        NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
