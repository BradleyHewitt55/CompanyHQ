using CompanyHQ.Appointments.Extensions;
using CompanyHQ.Auth.Extensions;
using CompanyHQ.Blogs.Extensions;
using CompanyHQ.Company.Extensions;
using CompanyHQ.Contact.Extensions;
using CompanyHQ.Email.Extensions;
using CompanyHQ.Faqs.Extensions;
using CompanyHQ.Geo.Extensions;
using CompanyHQ.GitHub.Extensions;
using CompanyHQ.GitHub.Contracts.Models;
using CompanyHQ.Inbox.Extensions;
using CompanyHQ.Mcp.Extensions;
using CompanyHQ.Media.Extensions;
using CompanyHQ.Messages.Extensions;
using CompanyHQ.Moderation.Extensions;
using CompanyHQ.Notifications.Extensions;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Rss.Extensions;
using CompanyHQ.Sales.Extensions;
using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Shipping.Extensions;
using CompanyHQ.Users.Extensions;
using CompanyHQ.Widget.Extensions;
using CompanyHQ.Widget.Contracts.Ports;

namespace Api.Integrations;

/// <summary>HQ's explicit feature allowlist. Consumer hosts own their own selections and provider wiring.</summary>
public static class HqServiceCatalog
{
    public static string[] AllowedServices => ["appointments", "auth", "blogs", "company", "contact", "email", "faqs",
        "geo", "github", "inbox", "mcp", "media", "messages", "moderation", "notifications", "rss", "sales", "shipping", "users", "widget"];

    public static IServiceCollection AddHqServiceDescriptors(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAppointmentsServiceDescriptor();
        services.AddAuthServiceDescriptor();
        services.AddBlogsServiceDescriptor();
        services.AddCompanyServiceDescriptor();
        services.AddContactServiceDescriptor();
        var email = configuration["ServiceProviders:Email"] ?? "resend";
        if (email is not ("resend" or "localsmtp")) throw new InvalidOperationException("Unknown Email provider.");
        services.AddEmailServiceDescriptor(email);
        services.AddFaqsServiceDescriptor();
        services.AddGeoServiceDescriptor();
        services.AddGitHubServiceDescriptor();
        services.AddInboxServiceDescriptor();
        services.AddMcpServiceDescriptor();
        var media = configuration["ServiceProviders:Media"] ?? "s3";
        if (media is not ("s3" or "local")) throw new InvalidOperationException("Unknown Media provider.");
        services.AddMediaServiceDescriptor(media);
        services.AddMessagesServiceDescriptor();
        services.AddModerationServiceDescriptor();
        services.AddNotificationsServiceDescriptor();
        services.AddRssServiceDescriptor();
        services.AddSalesServiceDescriptor();
        services.AddShippingServiceDescriptor();
        services.AddUsersServiceDescriptor();
        services.AddWidgetServiceDescriptor(isCapabilityConfigured: async (provider, capability, token) =>
        {
            if (capability == "conversations") return services.Any(item => item.ServiceType == typeof(IWidgetConversations));
            return services.Any(item => item.ServiceType == typeof(IWidgetIssueTracker))
                && await provider.GetRequiredService<IPersistence>().Query<GitHubConnection>()
                    .AnyAsync(connection => connection.Id == GitHubConnection.SingletonId, token);
        });
        services.AddServiceConfigurationKeys("email", new ServiceConfigurationKey("ServiceProviders:Email", "Host-installed email transport; provider changes require restart.", Required: false));
        services.AddServiceConfigurationKeys("media", new ServiceConfigurationKey("ServiceProviders:Media", "Host-installed storage transport; historical objects require migration before changing it.", Required: false, ReadOnly: true,
            ProtectionReason: "Existing objects have no provider affinity."));
        services.AddServiceConfigurationKeys("notifications", new ServiceConfigurationKey("ServiceCatalog:LoadFirebase", "Load active Firebase credentials. False keeps push explicitly unconfigured.", Required: false));
        return services;
    }
}
