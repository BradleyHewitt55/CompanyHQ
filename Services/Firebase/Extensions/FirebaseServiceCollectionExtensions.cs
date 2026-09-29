using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Firebase.Contracts.Models;
using CompanyHQ.Services.Firebase.Services;
using CompanyHQ.Services.Notifications.Contracts.Ports;

namespace CompanyHQ.Services.Firebase.Extensions;

/// <summary>Registers Firebase as the adapter for Notification's push port.</summary>
public static class FirebaseServiceCollectionExtensions
{
    /// <summary>
    /// Registers the FCM transport. Credentials are optional: without them the transport warns and
    /// disables push delivery, so a local API can boot without a private service-account key.
    /// </summary>
    public static IServiceCollection AddFirebase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(FirebaseOptions.SectionName).Get<FirebaseOptions>() ?? new();
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IFirebaseCredentialProvider, FirebaseCredentialProvider>();
        services.AddSingleton<IPushSender, FcmSender>();

        return services;
    }
}
