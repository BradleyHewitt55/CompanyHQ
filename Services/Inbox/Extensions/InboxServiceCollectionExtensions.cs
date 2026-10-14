using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Inbox.Contracts.Ports;
using CompanyHQ.Inbox.Services;

namespace CompanyHQ.Inbox.Extensions;

/// <summary>Registers the staff inbox, its built-in visitor conversations, and the controller assembly.</summary>
public static class InboxServiceCollectionExtensions
{
    public static IServiceCollection AddInbox(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // One scoped instance is both the visitor half and the built-in channel over the same rows.
        services.AddScoped<VisitorConversationService>();
        services.AddScoped<IVisitorConversationService>(provider => provider.GetRequiredService<VisitorConversationService>());
        services.AddScoped<IInboxChannel>(provider => provider.GetRequiredService<VisitorConversationService>());

        services.AddScoped<IInboxService, InboxService>();
        services.AddHostedService<InboxRetentionWorker>();

        services.AddControllers().AddApplicationPart(typeof(InboxServiceCollectionExtensions).Assembly);

        return services;
    }
}
