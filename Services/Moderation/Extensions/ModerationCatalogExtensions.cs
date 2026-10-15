using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Moderation.Extensions;

public static class ModerationCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddModerationServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "moderation", "Moderation", "Reports, blocking and account safety.",
            "Members report content and block other accounts; staff review reports and restrict accounts. Required while Messages is on.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: "/moderation", Capabilities: null, ReadPermission: "moderation.manage"),
            typeof(ModerationCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
