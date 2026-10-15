using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Email.Extensions;

public static class EmailCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddEmailServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "resend", Func<IServiceProvider, bool>? isConfigured = null)
    {
        var providerType = services.LastOrDefault(item => item.ServiceType == typeof(CompanyHQ.Email.Contracts.Ports.IEmailProvider))?.ImplementationType;
        return
        services.AddServiceDescriptor(new(new(
            "email", "Email", "Send account and order emails.",
            "Sends verification, password-reset, notification and order emails from editable templates, retrying failed deliveries. Delivery uses the selected provider (Resend or local SMTP); set its keys in Environment. Always on, because sign-in depends on it.",
            Core: true, Dependencies: [],
            Providers: [
                new("resend", "Resend", "Transactional email through the official Resend SDK.", [
                    new("Resend:ApiKey", "Resend API key.", Secret: true, Required: true, ReadOnly: false)
                ]),
                new("localsmtp", "Local SMTP", "Company-owned SMTP transport; register the company provider.", [
                    new("LocalSMTP:Host", "SMTP server.", Secret: false, Required: true, ReadOnly: false),
                    new("LocalSMTP:Username", "SMTP username.", Secret: false, Required: false, ReadOnly: false),
                    new("LocalSMTP:Password", "SMTP password.", Secret: true, Required: false, ReadOnly: false)
                ])
            ],
            DefaultProvider: "resend", MenuPath: "/email-templates", Capabilities: null, ReadPermission: "list_email_templates",
            ConfigurationKeys: [
                new("Email:From", "Sender address used by every transport; Resend requires a verified sender.", Required: true),
                new("Email:MaxSendsPerSecond", "Max Sends Per Second.", Required: false)
            ]),
            typeof(EmailCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (provider => !string.IsNullOrWhiteSpace(provider.GetRequiredService<CompanyHQ.Email.Contracts.Models.EmailOptions>().From)
                && providerType is { } transport
                && transport != typeof(CompanyHQ.Email.Services.UnconfiguredEmailProvider))));
    }
}
