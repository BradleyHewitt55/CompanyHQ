using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Notifications.Contracts.Models;
using CompanyHQ.Services.Notifications.Contracts.Ports;
using CompanyHQ.Services.Notifications.Services;

namespace CompanyHQ.Services.Notifications.Extensions;

/// <summary>Registers the notification queue, worker, and protected device endpoints.</summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>
    /// Registers the queue, delivery worker, authenticated notification routes, and push through
    /// Firebase Cloud Messaging. Firebase credentials are optional: without them push delivery warns
    /// once and is disabled, so a local API can boot without a private service-account key.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var firebase = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new();
        firebase.Validate();

        services.AddSingleton(firebase);
        services.AddSingleton<FirebaseCredentialProvider>();
        services.AddSingleton<IPushSender, FcmSender>();

        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationAccountService, NotificationAccountService>();
        services.AddScoped<INotificationDeliveryService, NotificationDeliveryService>();
        services.AddScoped<NotificationQueueProcessor>();
        services.AddHostedService<NotificationWorker>();
        services.AddControllers()
            .AddApplicationPart(typeof(NotificationServiceCollectionExtensions).Assembly);

        return services;
    }
}
