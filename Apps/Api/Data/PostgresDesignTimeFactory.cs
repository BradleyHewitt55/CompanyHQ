using Microsoft.EntityFrameworkCore.Design;
using CompanyHQ.Postgres.Data;
using CompanyHQ.Postgres.Extensions;

namespace Api.Data;

/// <summary>
/// Lets <c>dotnet ef migrations add</c> build a context without starting the API.
/// </summary>
/// <remarks>
/// It discovers models from this assembly's dependency graph exactly as <see cref="Program"/> does,
/// so a generated migration can never drop a table the running host expects.
/// </remarks>
public sealed class PostgresDesignTimeFactory : IDesignTimeDbContextFactory<PostgresDbContext>
{
    public PostgresDbContext CreateDbContext(string[] args)
    {
        // `dotnet ef` does not go through Program, so the .env credentials must be loaded here too.
        EnvironmentFile.Load();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        return PostgresServiceCollectionExtensions.CreateDesignTimeContext(
            configuration,
            typeof(PostgresDesignTimeFactory).Assembly);
    }
}
