using Abstractions.Plugins;
using CompanyHQ.Services.Auth;
using CompanyHQ.Services.Companies;
using CompanyHQ.Services.Postgress;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    public const string AdminConnection =
        "Host=localhost;Database=postgres;Username=companyhq;Password=companyhq";
    private readonly List<(string Database, string? Role)> resources = [];

    public PostgresFixture()
    {
        ServerRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "..", "MockServer"));
        if (!File.Exists(Path.Combine(ServerRoot, ".gitignore")))
        {
            throw new InvalidOperationException($"Refusing to clean unexpected server root '{ServerRoot}'.");
        }

        var companies = Path.Combine(ServerRoot, "Companies");
        if (Directory.Exists(companies))
        {
            Directory.Delete(companies, recursive: true);
        }
    }

    public string ServerRoot { get; }

    public ServiceProvider CreateProvider()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["CompanyHq:ServerRoot"] = ServerRoot,
                ["ConnectionStrings:CompanyHqServer"] = AdminConnection,
            }).Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IConfiguration>(configuration);
        ICompanyHqPlugin[] plugins = [new CompaniesPlugin(), new PostgressPlugin(), new AuthPlugin()];
        foreach (var plugin in plugins) plugin.ConfigureServices(services, configuration);
        return services.BuildServiceProvider();
    }

    public async Task<string> CreateExternalDatabaseAsync()
    {
        var database = $"companyhq_it_{Guid.NewGuid():N}";
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{database}\"", connection);
        await command.ExecuteNonQueryAsync();
        resources.Add((database, null));
        return new NpgsqlConnectionStringBuilder(AdminConnection) { Database = database }.ConnectionString;
    }

    public void TrackManagedCompany(string company)
    {
        var identifier = new string(company.ToLowerInvariant()
            .Select(c => char.IsAsciiLetterOrDigit(c) ? c : '_').ToArray());
        resources.Add(($"companyhq_{identifier}", $"companyhq_{identifier}"));
    }

    public Task InitializeAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(AdminConnection);
        await connection.OpenAsync();
        foreach (var resource in resources)
        {
            await using (var dropDatabase = new NpgsqlCommand(
                $"DROP DATABASE IF EXISTS \"{resource.Database}\" WITH (FORCE)", connection))
            {
                await dropDatabase.ExecuteNonQueryAsync();
            }
            if (resource.Role is not null)
            {
                await using var dropRole = new NpgsqlCommand(
                    $"DROP ROLE IF EXISTS \"{resource.Role}\"", connection);
                await dropRole.ExecuteNonQueryAsync();
            }
        }

        var companies = Path.Combine(ServerRoot, "Companies");
        if (Directory.Exists(companies))
        {
            Directory.Delete(companies, recursive: true);
        }
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
