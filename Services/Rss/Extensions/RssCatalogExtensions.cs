using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Rss.Extensions;

public static class RssCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddRssServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "rss", "RSS", "Read news feeds and CompanyHQ releases.",
            "Reads RSS feeds inside the API. It is how the Update script finds new CompanyHQ releases, so it is always on and has no page of its own.",
            Core: true, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: null, Capabilities: null,
            ConfigurationKeys: [
                new("Rss:UserAgent", "User Agent.", Required: false),
                new("Rss:TimeoutSeconds", "Timeout Seconds.", Required: false),
                new("Rss:ConnectTimeoutSeconds", "Connect Timeout Seconds.", Required: false),
                new("Rss:MaxRedirects", "Max Redirects.", Required: false),
                new("Rss:MaxFeedBytes", "Max Feed Bytes.", Required: false),
                new("Rss:MaxPageBytes", "Max Page Bytes.", Required: false),
                new("Rss:MaxItems", "Max Items.", Required: false),
                new("Rss:MaxDiscoveredFeeds", "Max Discovered Feeds.", Required: false),
                new("Rss:AllowedPorts", "Allowed Ports.", Required: false)
            ]),
            typeof(RssCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
