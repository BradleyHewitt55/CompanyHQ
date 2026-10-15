using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Faqs.Extensions;

public static class FaqsCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddFaqsServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "faqs", "FAQs", "Answer common questions on your website.",
            "Write and order frequently asked questions and publish them on your site. Turning it off removes the FAQ from your site and admin; entries are kept.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/faqs", Capabilities: null, ReadPermission: "list_faq_entries"),
            typeof(FaqsCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
