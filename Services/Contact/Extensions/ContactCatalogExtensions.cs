using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Contact.Extensions;

public static class ContactCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddContactServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "contact", "Contact", "Receive enquiries from a contact form.",
            "Visitors send messages from your website, delivered by email to your support and information addresses. Needs Company and Email.",
            Core: false, Dependencies: ["company", "email"],
            Providers: null,
            DefaultProvider: null, MenuPath: null, Capabilities: null),
            typeof(ContactCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
