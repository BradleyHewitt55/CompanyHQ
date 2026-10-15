using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Messages.Extensions;

public static class MessagesCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddMessagesServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "messages", "Messages", "Private conversations between members.",
            "Members message each other and staff, with attachments and live updates. Needs Moderation so conversations can be reported and blocked.",
            Core: false, Dependencies: ["moderation"],
            Providers: null,
            DefaultProvider: null, MenuPath: "/messages", Capabilities: null, ReadPermission: "conversations.manage",
            ConfigurationKeys: [
                new("Message:Access:Close", "Close.", Required: false),
                new("Message:Access:Delete", "Delete.", Required: false),
                new("Message:Access:RequireVerifiedEmail", "Require Verified Email.", Required: false),
                new("Message:Spam:VolumeLimit", "Volume Limit.", Required: false),
                new("Message:Spam:SpreadLimit", "Spread Limit.", Required: false),
                new("Message:Spam:WindowMinutes", "Window Minutes.", Required: false),
                new("Message:Spam:EvidenceSampleSize", "Evidence Sample Size.", Required: false)
            ]),
            typeof(MessagesCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
