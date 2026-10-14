using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Company.Contracts.Ports;
using CompanyHQ.Company.Services;

namespace CompanyHQ.Company.Extensions;

/// <summary>Registers the company profile service and its controller assembly.</summary>
public static class CompanyServiceCollectionExtensions
{
    public static IServiceCollection AddCompany(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<ICompanyProfileService, CompanyProfileService>();

        // The read port other services use; the same scoped instance.
        services.AddScoped<ICompanyProfileReader>(provider => provider.GetRequiredService<ICompanyProfileService>());
        services.AddControllers().AddApplicationPart(typeof(CompanyServiceCollectionExtensions).Assembly);

        return services;
    }
}
