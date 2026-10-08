using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Mvc.Testing;
using Northstar.Demo;
using Testcontainers.PostgreSql;
using Xunit;

namespace Northstar.Tests;

/// <summary>
/// Boots the whole company API in memory against a disposable Postgres and Redis, so a test exercises
/// the generic packages and the company services exactly as the host wires them.
/// </summary>
// The marker is any type from the company host; `Program` also exists in the CompanyHQ.Api package.
public sealed class ApiFactory : WebApplicationFactory<LocalEmailProvider>, IAsyncLifetime
{
    private const string DatabasePassword = "company-tests-password";

    private readonly PostgreSqlContainer database = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("company_tests")
        .WithUsername("company")
        .WithPassword(DatabasePassword)
        .Build();

    private readonly IContainer cache = new ContainerBuilder()
        .WithImage("redis:7.4-alpine")
        .WithPortBinding(6379, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("redis-cli", "ping"))
        .Build();

    public async Task InitializeAsync()
    {
        await Task.WhenAll(database.StartAsync(), cache.StartAsync());

        // The host binds these while it registers services, before a test host could override
        // configuration, so they go in the environment. A developer's .env never replaces a variable
        // that is already set, which keeps the tests off the real database.
        Set("Postgres__ConnectionString",
            $"Host={database.Hostname};Port={database.GetMappedPublicPort(5432)};Database=company_tests;Username=company;Password={DatabasePassword}");
        Set("Postgres__Password", DatabasePassword);
        Set("Cache__ConnectionString", $"{cache.Hostname}:{cache.GetMappedPublicPort(6379)}");
        Set("Jwt__SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
        Set("Demo__StorageSigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await Task.WhenAll(database.DisposeAsync().AsTask(), cache.DisposeAsync().AsTask());
    }

    private static void Set(string name, string value) => Environment.SetEnvironmentVariable(name, value);

    /// <summary>Registers a new account and returns a client that carries its access token.</summary>
    public async Task<HttpClient> RegisterAsync(string label)
    {
        var client = CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            email = $"{label}-{Guid.NewGuid():N}@northstar.test",
            password = $"{Guid.NewGuid():N}Aa1!",
            firstName = label,
            lastName = "Tester",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());
        return client;
    }
}
