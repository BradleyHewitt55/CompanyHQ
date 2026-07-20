using Abstractions.Companies;
using Abstractions.Data;
using Abstractions.Users;
using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace UnitTests;

public sealed class CompanyCreationTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), $"companyhq-flow-{Guid.NewGuid():N}");

    [Fact]
    public async Task RePromptsInvalidAdminAndDatabaseInput()
    {
        var registry = new Registry();
        var databases = new Databases();
        var userService = new Users();
        var paths = new ServerPaths(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["CompanyHq:ServerRoot"] = root }).Build());
        var services = new ServiceCollection()
            .AddScoped<ICompanyContext, CompanyContext>()
            .AddSingleton<IUserService>(userService)
            .BuildServiceProvider();
        var console = new ScriptedConsole(
            ["", "Ada", "Lovelace", "bad", "ada@example.com", "", "123", "3", "1"],
            ["", "pw", "long-enough-pw", "different", "long-enough-pw", "long-enough-pw"]);
        var creation = new CompanyCreation(registry, databases, paths, services.GetRequiredService<IServiceScopeFactory>());

        await creation.CreateAsync("Acme", console);

        Assert.Equal("Acme", registry.Company);
        Assert.Equal(("Acme", null), databases.Ensured);
        Assert.Equal(new NewUser("Ada", "Lovelace", "ada@example.com", "123", "long-enough-pw"), userService.Seeded);
        Assert.Contains(console.Output, line => line.Contains("valid email", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(console.Output, line => line.Contains(PasswordPolicy.Requirement, StringComparison.Ordinal));
        Assert.Contains(console.Output, line => line.Contains("Passwords do not match", StringComparison.Ordinal));
        Assert.Contains(console.Output, line => line.Contains("Choose 1 or 2", StringComparison.Ordinal));
        await services.DisposeAsync();
    }

    public void Dispose()
    {
        if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
    }

    private sealed class ScriptedConsole(IEnumerable<string?> lines, IEnumerable<string?> secrets) : ICompanyConsole
    {
        private readonly Queue<string?> lines = new(lines);
        private readonly Queue<string?> secrets = new(secrets);
        public List<string> Output { get; } = [];
        public Task<string?> ReadLineAsync(CancellationToken cancellationToken = default) => Task.FromResult(lines.Dequeue());
        public Task<string?> ReadSecretAsync(CancellationToken cancellationToken = default) => Task.FromResult(secrets.Dequeue());
        public Task WriteAsync(string value, CancellationToken cancellationToken = default) { Output.Add(value); return Task.CompletedTask; }
        public Task WriteLineAsync(string value, CancellationToken cancellationToken = default) { Output.Add(value); return Task.CompletedTask; }
    }

    private sealed class Registry : ICompanyRegistry
    {
        public string? Company { get; private set; }
        public Task<IReadOnlyList<string>> ListAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<string>>([]);
        public Task RegisterAsync(string companyName, string? externalConnectionString = null, CancellationToken cancellationToken = default)
        {
            Company = companyName;
            return Task.CompletedTask;
        }
    }

    private sealed class Databases : ICompanyDatabases
    {
        public (string Company, string? Connection)? Ensured { get; private set; }
        public bool IsConfigured(string companyName) => Ensured?.Company == companyName;
        public Task EnsureDatabaseAsync(string companyName, string? externalConnectionString = null, CancellationToken cancellationToken = default)
        {
            Ensured = (companyName, externalConnectionString);
            return Task.CompletedTask;
        }
    }

    private sealed class Users : IUserService
    {
        public NewUser? Seeded { get; private set; }
        public Task<User> SeedAdminAsync(NewUser details, CancellationToken cancellationToken = default)
        {
            Seeded = details;
            return Task.FromResult(new User { Name = details.Name, Surname = details.Surname, Email = details.Email, ContactNumber = details.ContactNumber, Role = Roles.Admin, PasswordHash = "hash" });
        }
        public Task<User?> GetAsync(Guid id, CancellationToken cancellationToken = default) => Task.FromResult<User?>(null);
        public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<User>> ListAsync(CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<PagedResult<User>> QueryAsync(UserQuery query, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<User> SaveAsync(UserSave details, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> AnyAsync(CancellationToken cancellationToken = default) => Task.FromResult(Seeded is not null);
    }
}
