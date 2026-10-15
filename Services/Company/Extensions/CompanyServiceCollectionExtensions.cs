using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Company.Contracts.Ports;
using CompanyHQ.Company.Services;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;

namespace CompanyHQ.Company.Extensions;

/// <summary>Registers the company profile service and its controller assembly.</summary>
public static class CompanyServiceCollectionExtensions
{
    /// <summary>Opt-in runtime catalog management, independent of the public profile.</summary>
    public static IServiceCollection AddCompanyServiceManagement(this IServiceCollection services)
    {
        services.AddScoped<CompanyHQ.ServiceCatalog.Contracts.IServiceSelectionReader, CompanyServiceSelectionReader>();
        services.AddScoped<ICompanyServiceCatalog, CompanyServiceCatalog>();
        services.AddScoped<ICompanyServiceNavigation, CompanyServiceNavigation>();
        services.AddControllers().ConfigureApplicationPartManager(manager =>
            manager.FeatureProviders.Add(new CompanyManagementControllerProvider()));
        return services;
    }

    public static IServiceCollection AddCompany(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICompanyProfileService, CompanyProfileService>();

        // The read port other services use; the same scoped instance.
        services.AddScoped<ICompanyProfileReader>(provider => provider.GetRequiredService<ICompanyProfileService>());
        services.AddControllers().AddApplicationPart(typeof(CompanyServiceCollectionExtensions).Assembly);

        return services;
    }

    private sealed class CompanyManagementControllerProvider : IApplicationFeatureProvider<ControllerFeature>
    {
        public void PopulateFeature(IEnumerable<ApplicationPart> parts, ControllerFeature feature)
        {
            feature.Controllers.Add(typeof(CompanyHQ.Company.Controllers.CompanyServicesController).GetTypeInfo());
            feature.Controllers.Add(typeof(CompanyHQ.Company.Controllers.CompanyServiceNavigationController).GetTypeInfo());
        }
    }
}
