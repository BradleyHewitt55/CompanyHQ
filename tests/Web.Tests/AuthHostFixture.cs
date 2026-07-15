using System.Text.RegularExpressions;
using Services.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using Services.Orders;
using Services.Sales;
using Services.Quotes;
using Services.Products;

namespace Web.Tests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class AuthCollection : ICollectionFixture<AuthHostFixture>
{
    public const string Name = "Auth host";
}

public sealed partial class AuthHostFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private string folder = null!;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;
    public string OwnerUsername { get; } = "owner@company.test";
    public string OwnerPassword { get; private set; } = null!;
    public string ConnectionString => postgres.GetConnectionString();
    public string RootFolder => folder;
    public CapturingLoggerProvider Logs { get; } = new();

    public async Task InitializeAsync()
    {
        await postgres.StartAsync();
        folder = Path.Combine(Path.GetTempPath(), "companyhq-auth-" + Guid.NewGuid());
        Directory.CreateDirectory(Path.Combine(folder, "modules"));
        Directory.CreateDirectory(Path.Combine(folder, "keys"));
        var sales = Path.Combine(folder, "src", "Services", "Sales");
        Directory.CreateDirectory(Path.Combine(sales, "Services"));
        File.WriteAllText(Path.Combine(sales, "sales.json"), "{\"id\":\"sales\",\"name\":\"Sales\",\"version\":\"1.0.0\",\"author\":\"test\",\"icon\":\"sales\",\"color\":\"#000\",\"tags\":[],\"description\":\"Sales\",\"menu\":[]}");
        CopyModule(typeof(SalesModule).Assembly.Location, "sales.json");
        CopyModule(typeof(OrdersModule).Assembly.Location, "orders.json");
        CopyModule(typeof(QuotesModule).Assembly.Location, "quotes.json");
        CopyModule(typeof(ProductsModule).Assembly.Location, "products.json");
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ModulesPath", Path.Combine(folder, "modules"));
            builder.UseSetting("Database:Provider", "postgres");
            builder.UseSetting("ConnectionStrings:Company", postgres.GetConnectionString());
            builder.UseSetting("ConnectionStrings:Registry", $"Data Source={Path.Combine(folder, "companies.db")}");
            builder.UseSetting("Companies:Root", Path.Combine(folder, "companies"));
            builder.UseSetting("DataProtection:Path", Path.Combine(folder, "keys"));
            builder.UseSetting("Company:Id", Guid.NewGuid().ToString());
            builder.UseSetting("Scaffolding:Root", folder);
            builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        });
        _ = Factory.Services;
        await using var scope = Factory.Services.CreateAsyncScope();
        var seeded = await scope.ServiceProvider.GetRequiredService<AdminUserSeeder>().SeedOwner(OwnerUsername);
        OwnerPassword = seeded.Password!;
    }

    public HttpClient CreateClient(bool autoRedirect = false, bool cookies = true) => Factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = autoRedirect,
        HandleCookies = cookies
    });

    private void CopyModule(string assembly, string manifest)
    {
        File.Copy(assembly, Path.Combine(folder, "modules", Path.GetFileName(assembly)), true);
        File.Copy(Path.Combine(Path.GetDirectoryName(assembly)!, manifest), Path.Combine(folder, "modules", manifest), true);
    }

    public async Task<HttpResponseMessage> Login(HttpClient client, string username, string password, string returnUrl = "/admin/dashboard")
    {
        var token = await GetAntiforgeryToken(client, returnUrl);
        return await client.PostAsync("/auth/login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Username"] = username,
            ["Password"] = password,
            ["ReturnUrl"] = returnUrl,
            ["__RequestVerificationToken"] = token
        }));
    }

    public async Task<string> GetAntiforgeryToken(HttpClient client, string returnUrl = "/admin/dashboard")
    {
        var html = await client.GetStringAsync($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        return TokenRegex().Match(html).Groups[1].Value;
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        await postgres.DisposeAsync();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(folder, true);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}

public sealed class CapturingLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<string> messages = new();
    public IReadOnlyCollection<string> Messages => [.. messages];
    public ILogger CreateLogger(string categoryName) => new Logger(messages);
    public void Dispose() { }
    private sealed class Logger(ConcurrentQueue<string> messages) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) => messages.Enqueue(formatter(state, exception));
    }
}
