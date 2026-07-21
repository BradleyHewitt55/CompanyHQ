using Abstractions.Companies;
using Abstractions.Data;
using Abstractions.Plugins;
using CompanyHQ.Services.Postgress.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Postgress;

public sealed class PostgressPlugin : ICompanyHqPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        "companyhq.postgress",
        "Postgress",
        "PostgreSQL persistence and per-company schema synchronization.",
        new Version(1, 0, 0),
        PluginOrigin.BuiltIn,
        PluginActivation.Required,
        ["companyhq.companies"]);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        // Loading this plugin IS the provider choice: consumers only ever see the
        // Abstractions.Data interfaces, so another database means another plugin, not code changes.
        services.AddSingleton<CompanyDatabases>();
        services.AddSingleton<ICompanyDatabases>(sp => sp.GetRequiredService<CompanyDatabases>());
        services.AddSingleton<SchemaSynchronizer>();

        // The connection string is resolved per scope from the ambient company:
        // the current company's db.json, or the platform database when no company
        // is selected. Options must be scoped too, or the first tenant's
        // connection would be cached for everyone.
        services.AddDbContext<PostgressDbContext>(
            (sp, options) => options.UseNpgsql(
                sp.GetRequiredService<CompanyDatabases>()
                    .Resolve(sp.GetService<ICompanyContext>()?.Current)),
            contextLifetime: ServiceLifetime.Scoped,
            optionsLifetime: ServiceLifetime.Scoped);

        services.AddScoped(typeof(IRepository<>), typeof(PostgressRepositories<>));
        services.AddScoped(typeof(IQueryService<>), typeof(PostgressQueries<>));

        // Hosted services run before the host serves traffic, so every database is
        // synced to the loaded plugin set before any consumer can touch a store.
        services.AddHostedService<SchemaMigrator>();
    }
}
