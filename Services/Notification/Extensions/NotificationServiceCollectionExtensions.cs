using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Notifications.Contracts.Ports;
using CompanyHQ.Services.Notifications.Services;

namespace CompanyHQ.Services.Notifications.Extensions;

/// <summary>Registers the notification queue, worker, and protected device endpoints.</summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>Registers the queue, delivery worker, and authenticated notification routes for the API host.</summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
