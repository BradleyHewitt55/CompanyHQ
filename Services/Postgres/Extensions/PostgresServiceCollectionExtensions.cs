using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;
using Npgsql;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Postgres.Contracts.Models;
using CompanyHQ.Postgres.Data;
using CompanyHQ.Postgres.Services;

namespace CompanyHQ.Postgres.Extensions;

/// <summary>
/// Single registration entry point for the CompanyHQ Postgres service.
/// </summary>
public static class PostgresServiceCollectionExtensions
{
    /// <summary>
    /// Registers the shared data source and <see cref="PostgresDbContext"/>, fills the Persistence
    /// ports (<see cref="IPersistence"/> and <see cref="IRepository{TModel}"/>) with them, and adds
    /// the startup migration service and the <c>postgres</c> health check.
    /// </summary>
    /// <param name="hostAssembly">
    /// The API host: it holds the migrations, and its dependency graph is where every model is
    /// discovered — see <see cref="PostgresModelRegistry.Discover"/>.
    /// </param>
    public static IServiceCollection AddPostgres(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly hostAssembly)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(hostAssembly);

        var options = BindOptions(configuration);

        services.AddSingleton(options);
        services.AddSingleton(PostgresModelRegistry.Discover(hostAssembly));
        services.AddSingleton(_ => CreateDataSource(options));

        services.AddDbContext<PostgresDbContext>((provider, contextOptions) => ConfigureContext(
            contextOptions,
            provider.GetRequiredService<NpgsqlDataSource>(),
            options,
            hostAssembly));

        // The provider-neutral ports every CompanyHQ service persists through. They share the one
        // scoped context, so a repository write and a unit-of-work save see each other.
        services.AddScoped<IPersistence, PostgresPersistence>();
        services.AddScoped(typeof(IRepository<>), typeof(PostgresRepository<>));

        // This provider's own surface, for code that deliberately depends on PostgreSQL.
        services.AddScoped(typeof(IPostgresService<>), typeof(PostgresService<>));

        // One hosted service owns the advisory lock, migration, and seeding sequence. Registering
        // them separately would let another host enter between migration and seed.
        services.AddHostedService<PostgresMigrationService>();

        services.AddHealthChecks().AddCheck<PostgresHealthCheck>(
            PostgresHealthCheck.Name,
            failureStatus: HealthStatus.Unhealthy,
            tags: ["db", "postgres", "ready"]);

        return services;
    }

    /// <summary>
    /// Builds a context outside the host's container, for <c>dotnet ef</c> design-time factories.
    /// It binds, validates, and discovers exactly as <see cref="AddPostgres"/> does, which is what
    /// stops generated migrations from drifting away from the schema the running host expects.
    /// </summary>
    public static PostgresDbContext CreateDesignTimeContext(IConfiguration configuration, Assembly hostAssembly)
    {
        var options = BindOptions(configuration);
        var contextOptions = new DbContextOptionsBuilder<PostgresDbContext>();
        ConfigureContext(contextOptions, CreateDataSource(options), options, hostAssembly);

        return new PostgresDbContext(contextOptions.Options, PostgresModelRegistry.Discover(hostAssembly));
    }

    // Fail during startup rather than on the first query, where the stack trace is useless.
    private static PostgresOptions BindOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(PostgresOptions.SectionName).Get<PostgresOptions>() ?? new();
        options.Validate();

        return options;
    }

    private static NpgsqlDataSource CreateDataSource(PostgresOptions options)
    {
        var dataSourceBuilder = new NpgsqlDataSourceBuilder(options.BuildConnectionString());
        // NetTopologySuite supplies the PostGIS type mappings the geo models depend on.
        dataSourceBuilder.UseNetTopologySuite();

        return dataSourceBuilder.Build();
    }

    private static readonly PostgresRowLockInterceptor RowLocks = new();

    private static void ConfigureContext(
        DbContextOptionsBuilder contextOptions,
        NpgsqlDataSource dataSource,
        PostgresOptions options,
        Assembly migrationsAssembly)
    {
        contextOptions.UseNpgsql(dataSource, npgsql =>
        {
            npgsql.UseNetTopologySuite();
            npgsql.MigrationsAssembly(migrationsAssembly.FullName);
            npgsql.CommandTimeout(options.CommandTimeoutSeconds);
            npgsql.EnableRetryOnFailure(
                options.MaxRetryCount,
                TimeSpan.FromSeconds(options.MaxRetryDelaySeconds),
                errorCodesToAdd: null);
        });

        // Postgres folds unquoted identifiers to lower case, so snake_case keeps hand-written SQL
        // free of quoted "PascalCase" names.
        contextOptions.UseSnakeCaseNamingConvention();

        // EF Core cannot express FOR UPDATE; this adds it to the queries IPersistence.Locked marks.
        contextOptions.AddInterceptors(RowLocks);

        if (options.EnableSensitiveDataLogging)
        {
            contextOptions.EnableSensitiveDataLogging();
            contextOptions.EnableDetailedErrors();
        }
    }
}
