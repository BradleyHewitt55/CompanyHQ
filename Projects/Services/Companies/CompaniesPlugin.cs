using Abstractions;
using Abstractions.Companies;
using CompanyHQ.Services.Companies.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Companies;

public sealed class CompaniesPlugin : ICompanyHqPlugin
{
    public string Id => "companyhq.companies";
    public string Name => "Companies";
    public Version Version => new(1, 0, 0);

    public void ConfigureServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IServerPaths, ServerPaths>();
        services.AddSingleton<ICompanyRegistry, CompanyRegistry>();
        services.AddScoped<IServiceScaffolder, ServiceScaffolder>();
        services.AddScoped<IServiceCatalog, ServiceCatalog>();
        services.AddSingleton<ICompanyLocator, CompanyLocator>();
        services.AddTransient<CompanyCreation>();

        // Scoped so each request/admin session carries its own current company;
        // a singleton here would leak one tenant's selection into another's work.
        services.AddScoped<ICompanyContext, CompanyContext>();
    }
}
