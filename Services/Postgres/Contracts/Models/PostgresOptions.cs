using Npgsql;

namespace CompanyHQ.Postgres.Contracts.Models;

/// <summary>
/// Configuration for the shared CompanyHQ PostgreSQL connection, bound from the
/// <see cref="SectionName"/> configuration section.
/// </summary>
public sealed class PostgresOptions
{
    public const string SectionName = "Postgres";

    /// <summary>
    /// Full Npgsql connection string. Takes precedence over the composed host/port/database fields.
    /// </summary>
    public string? ConnectionString { get; set; }

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 5432;

    public string Database { get; set; } = "companyhq";

    public string Username { get; set; } = "companyhq";

    public string Password { get; set; } = string.Empty;

    /// <summary>Seconds a command may run before Npgsql cancels it.</summary>
    public int CommandTimeoutSeconds { get; set; } = 30;

    /// <summary>Transient-failure retries handled by EF Core's execution strategy.</summary>
    public int MaxRetryCount { get; set; } = 5;

    public int MaxRetryDelaySeconds { get; set; } = 10;

    public int MinPoolSize { get; set; } = 1;

    public int MaxPoolSize { get; set; } = 50;

    /// <summary>
    /// Applies pending migrations when the host starts. On by default so a fresh clone plus
    /// <c>docker compose up</c> is a working database; turn it off wherever two hosts could start
    /// at once and race each other.
    /// </summary>
    public bool AutoMigrate { get; set; } = true;

    /// <summary>
    /// Runs the registered idempotent seeders after migrations. Hosts that only consume the shared
    /// database, such as Web, must disable this as well as <see cref="AutoMigrate"/>.
    /// </summary>
    public bool AutoSeed { get; set; } = true;

    /// <summary>Logs parameter values and raw SQL. Leaks personal data, so development only.</summary>
    public bool EnableSensitiveDataLogging { get; set; }

    /// <summary>
    /// Builds the effective connection string, preferring <see cref="ConnectionString"/> and
    /// otherwise composing one from the individual fields.
    /// </summary>
    public string BuildConnectionString()
    {
        var builder = string.IsNullOrWhiteSpace(ConnectionString)
            ? new NpgsqlConnectionStringBuilder
            {
                Host = Host,
                Port = Port,
                Database = Database,
                Username = Username,
                Password = Password
            }
            : new NpgsqlConnectionStringBuilder(ConnectionString);

        builder.CommandTimeout = CommandTimeoutSeconds;
        builder.MinPoolSize = MinPoolSize;
        builder.MaxPoolSize = MaxPoolSize;

        return builder.ConnectionString;
    }

    /// <summary>Throws when the bound configuration cannot produce a usable connection.</summary>
    public void Validate()
    {
        if (!string.IsNullOrWhiteSpace(ConnectionString))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException($"{SectionName}:Host is required.");
        }

        if (string.IsNullOrWhiteSpace(Database))
        {
            throw new InvalidOperationException($"{SectionName}:Database is required.");
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            throw new InvalidOperationException($"{SectionName}:Username is required.");
        }

        // An empty password reaches the server as a trust-auth attempt and fails with a confusing
        // error, so fail here where the cause is obvious.
        if (string.IsNullOrEmpty(Password))
        {
            throw new InvalidOperationException(
                $"{SectionName}:Password is required. Set {SectionName}:Password or {SectionName}:ConnectionString.");
        }
    }
}
