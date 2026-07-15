using Services.Postgres.Providers;
using Services.Companies.Contracts.Data;

namespace Cli;

public static class DbProviders
{
    private static readonly IReadOnlyList<IDbProvider> Installed = [new PostgresDbProvider()];

    public static IDbProvider Resolve(string name) => Installed.FirstOrDefault(provider =>
        provider.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
        ?? throw new NotSupportedException($"Database provider '{name}' is not installed.");
}
