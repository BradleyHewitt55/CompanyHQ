using Abstractions.Companies;
using Abstractions.Plugins;
using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Companies;

public sealed class CompaniesPlugin : ICompanyHqPlugin
{
    public PluginDescriptor Descriptor { get; } = new(
        "companyhq.companies",
        "Companies",
        "Company registry, tenant context, paths, and service scaffolding.",
        new Version(1, 0, 0),
        PluginOrigin.BuiltIn,
        PluginActivation.Required,
        []);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IServerPaths, ServerPaths>();
        services.AddSingleton<ICompanyRegistry, CompanyRegistry>();
        services.AddScoped<IServiceScaffolder, ServiceScaffolder>();
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddSingleton<ICompanyLocator, CompanyLocator>();
        services.AddTransient<CompanyCreation>();

        // Every scope starts bound to the process company. This also covers Blazor
        // circuit and background scopes that never pass through HTTP middleware.
        var company = configuration["CompanyHq:Company"]
            ?? throw new InvalidOperationException(
                "CompanyHq:Company must be set — each host process serves exactly one company.");
        services.AddScoped<ICompanyContext>(_ => new CompanyContext { Current = company });
    }
}
