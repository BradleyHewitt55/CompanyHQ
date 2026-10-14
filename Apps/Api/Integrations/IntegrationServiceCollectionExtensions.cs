using CompanyHQ.Services.Auth.Services;
using CompanyHQ.Services.Blogs.Contracts.Ports;
using CompanyHQ.Services.Contact.Contracts.Ports;
using CompanyHQ.Services.Inbox.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Messages.Contracts.Ports;
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Notifications.Contracts.Ports;
using CompanyHQ.Services.Widget.Contracts.Ports;

namespace Api.Integrations;

/// <summary>
/// The only place two feature services meet. Each service declares the ports it needs in its own
/// assembly and never references another feature service; this host decides which ones are plugged
/// together. Drop a service from the host and delete its lines here — nothing else changes.
/// </summary>
public static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        // Auth and Contact cannot work without these, so startup validation fails if they are missing.
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddScoped<IContactAddressBook, CompanyContactAddressBook>();
        services.AddScoped<IContactMailer, EmailContactMailer>();

        services.AddScoped<IBlogCoverImages, BlogCoverImages>();

        // Optional channels: each service still runs when the line is removed.
        services.AddScoped<INotificationEmailSender, NotificationEmailSender>();
        services.AddScoped<IInboxReplyMailer, InboxReplyMailer>();

        // What the public widget feeds, and which other services show up as channels in the staff
        // inbox. A social account plugs in the same way: one more IInboxChannel line.
        services.AddScoped<IWidgetConversations, InboxWidgetConversations>();
        services.AddScoped<IWidgetIssueTracker, GitHubWidgetIssueTracker>();
        services.AddScoped<IInboxChannel, GitHubInboxChannel>();
        services.AddScoped<IInboxChannel, ConversationInboxChannel>();

        services.AddScoped<ModerationMessageSafety>();
        services.AddScoped<IMessageBlocks>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IMessageAccountFlags>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IMessageRestrictions>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IReportableContentSource, MessageReportSource>();
        services.AddScoped<IReportHistoryClearer, MessageReportHistoryClearer>();
        services.AddScoped<IReportHistoryClearer, UserReportMessageHistoryClearer>();

        services.AddScoped<IMediaAccessPolicy, MessageMediaAccessPolicy>();
        services.AddScoped<IMessageAttachments, MessageAttachments>();

        services.AddScoped<IMessageNotifier, MessagePushNotifier>();

        services.AddScoped<CompanyHQ.Services.Sales.Contracts.Ports.ISalesProfitShipping, SalesProfitShippingBridge>();
        services.AddScoped<CompanyHQ.Services.Sales.Contracts.Ports.ISalesDocumentDelivery, SalesDocumentDelivery>();
        services.AddScoped<CompanyHQ.Services.Sales.Contracts.Ports.ISalesShipping, SalesShippingBridge>();
        services.AddScoped<CompanyHQ.Services.Sales.Contracts.Ports.ISalesCustomer, SalesCustomerBridge>();
        services.AddScoped<CompanyHQ.Services.Shipping.Contracts.Ports.IShippingCatalog, ShippingCatalogBridge>();
        services.AddScoped<CompanyHQ.Services.Shipping.Contracts.Ports.IShippingDiscountPolicy, ShippingDiscountBridge>();
        services.AddScoped<CompanyHQ.Services.Shipping.Contracts.Ports.IShippingAddressDirectory, ShippingAddressDirectoryBridge>();
        services.AddScoped<CompanyHQ.Services.Shipping.Contracts.Ports.IShippingGeocoder, ShippingGeocoderBridge>();
        return services;
    }
}
