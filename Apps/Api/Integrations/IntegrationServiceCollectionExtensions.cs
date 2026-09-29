using CompanyHQ.Services.Auth.Services;
using CompanyHQ.Services.Contact.Contracts.Ports;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Messages.Contracts.Ports;
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Notifications.Contracts.Ports;

namespace Api.Integrations;

/// <summary>
/// The only place two feature services meet. Each service declares the ports it needs in its own
/// assembly and never references another feature service; this host decides which ones are plugged
/// together. Drop a service from the host and delete its lines here — nothing else changes.
/// </summary>
internal static class IntegrationServiceCollectionExtensions
{
    public static IServiceCollection AddIntegrations(this IServiceCollection services)
    {
        // Auth and Contact cannot work without these, so startup validation fails if they are missing.
        services.AddScoped<IAuthEmailSender, AuthEmailSender>();
        services.AddScoped<IContactAddressBook, CompanyContactAddressBook>();
        services.AddScoped<IContactMailer, EmailContactMailer>();

        // Optional channels: each service still runs when the line is removed.
        services.AddScoped<INotificationEmailSender, NotificationEmailSender>();

        services.AddScoped<ModerationMessageSafety>();
        services.AddScoped<IMessageBlocks>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IMessageAccountFlags>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IMessageRestrictions>(provider => provider.GetRequiredService<ModerationMessageSafety>());
        services.AddScoped<IReportableContentSource, MessageReportSource>();
        services.AddScoped<IReportHistoryClearer, MessageReportHistoryClearer>();
        services.AddScoped<IReportHistoryClearer, UserReportMessageHistoryClearer>();

        services.AddScoped<MessageMedia>();
        services.AddScoped<IMediaAccessPolicy>(provider => provider.GetRequiredService<MessageMedia>());
        services.AddScoped<IMessageAttachments>(provider => provider.GetRequiredService<MessageMedia>());

        services.AddScoped<IMessageNotifier, MessagePushNotifier>();

        return services;
    }
}
