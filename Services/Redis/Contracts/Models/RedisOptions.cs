using StackExchange.Redis;

namespace CompanyHQ.Services.Redis.Contracts.Models;

/// <summary>
/// The Redis connection, bound from the <see cref="SectionName"/> configuration section.
/// </summary>
/// <remarks>
/// It binds Cache's own section on purpose: <c>Cache:ConnectionString</c> was the connection key
/// before the provider moved here, and a deployment must not need new configuration to upgrade.
/// </remarks>
public sealed class RedisOptions
{
    public const string SectionName = "Cache";

    /// <summary>
    /// StackExchange.Redis configuration string — the one place the endpoint, password, database,
    /// SSL, and timeouts are set, e.g. <c>redis:6379,password=…,defaultDatabase=0</c>.
    /// </summary>
    public string ConnectionString { get; set; } = "localhost:6379";

    /// <summary>
    /// Parses <see cref="ConnectionString"/> and pins the two settings this adapter depends on, so
    /// no connection string can switch them off.
    /// </summary>
    public ConfigurationOptions BuildConfiguration()
    {
        var configuration = ConfigurationOptions.Parse(ConnectionString);

        // Reconnecting in the background beats throwing for the lifetime of the process because
        // Redis happened to be slow at startup.
        configuration.AbortOnConnectFail = false;

        // Prefix removal needs SCAN, which is only reachable through an admin connection.
        configuration.AllowAdmin = true;

        return configuration;
    }

    /// <summary>Throws when the bound configuration cannot produce a usable connection.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString))
        {
            throw new InvalidOperationException($"{SectionName}:ConnectionString is required.");
        }
    }
}
