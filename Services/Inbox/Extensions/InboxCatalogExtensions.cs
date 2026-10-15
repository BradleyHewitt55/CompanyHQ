using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Inbox.Extensions;

public static class InboxCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddInboxServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "inbox", "Inbox", "Answer visitor messages in one place.",
            "Staff read and reply to conversations started by website visitors, alongside any other connected channels. Turning it off hides the inbox; conversations are kept.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/inbox", Capabilities: null, ReadPermission: "inbox.manage"),
            typeof(InboxCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
