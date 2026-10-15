using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Notifications.Extensions;

public static class NotificationsCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddNotificationsServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "firebase", Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "notifications", "Notifications", "Push and in-app notifications.",
            "Sends notifications to members, respecting each member's preferences, and push notifications to their devices through Firebase. Push needs Firebase credentials in Environment.",
            Core: false, Dependencies: [],
            Providers: [
                new("firebase", "Firebase Cloud Messaging", "Push notifications through Firebase Admin.", [
                    new("Firebase:ProjectId", "Firebase project identifier.", Secret: false, Required: true, ReadOnly: false),
                    new("Firebase:ServiceAccountJson", "Service-account JSON or file path.", Secret: true, Required: true, ReadOnly: false)
                ])
            ],
            DefaultProvider: "firebase", MenuPath: null, Capabilities: null),
            typeof(NotificationsCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (provider => provider.GetRequiredService<CompanyHQ.Notifications.Services.FirebaseCredentialProvider>().IsConfigured)));
}
