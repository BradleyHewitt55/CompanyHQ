using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Notifications.Contracts.Models;
using CompanyHQ.Notifications.Contracts.Ports;
using CompanyHQ.Notifications.Services;

namespace CompanyHQ.Notifications.Extensions;

/// <summary>Registers the notification queue, worker, and protected device endpoints.</summary>
public static class NotificationServiceCollectionExtensions
{
    /// <summary>Opt-in provider-neutral boot. Inactive Firebase credentials are never loaded or validated.</summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration,
        bool allowUnconfigured, bool useFirebase = true)
    {
        if (!allowUnconfigured && useFirebase) return services.AddNotifications(configuration);
        return AddCore(services, configuration, useFirebase, strictCredentials: useFirebase
            && !string.IsNullOrWhiteSpace(configuration["Firebase:ProjectId"])
            && !string.IsNullOrWhiteSpace(configuration["Firebase:ServiceAccountJson"]), permitIncomplete: allowUnconfigured);
    }
    /// <summary>
    /// Registers the queue, delivery worker, authenticated notification routes, and push through
    /// Firebase Cloud Messaging. Firebase credentials are optional: without them push delivery warns
    /// once and is disabled, so a local API can boot without a private service-account key.
    /// </summary>
    public static IServiceCollection AddNotifications(this IServiceCollection services, IConfiguration configuration)
        => AddCore(services, configuration, useFirebase: true, strictCredentials: false);

    private static IServiceCollection AddCore(IServiceCollection services, IConfiguration configuration,
        bool useFirebase, bool strictCredentials, bool permitIncomplete = false)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var firebase = useFirebase ? configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new() : new();
        if (permitIncomplete && (string.IsNullOrWhiteSpace(firebase.ProjectId) || string.IsNullOrWhiteSpace(firebase.ServiceAccountJson)))
            firebase = new();
        firebase.Validate();
        if (strictCredentials) FirebaseCredentialProvider.ValidateConfigured(firebase);

        services.AddSingleton(firebase);
        services.AddSingleton<FirebaseCredentialProvider>();
        services.AddSingleton(new FirebaseRegistrationPolicy(strictCredentials));
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
