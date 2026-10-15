using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Company.Extensions;

public static class CompanyCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddCompanyServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "company", "Company", "Your company profile and settings.",
            "Holds the company name, logo, website, public email addresses and social links shown on your site, and the switches on this page. Always on.",
            Core: true, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/company", Capabilities: null, ReadPermission: "company.manage"),
            typeof(CompanyCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
