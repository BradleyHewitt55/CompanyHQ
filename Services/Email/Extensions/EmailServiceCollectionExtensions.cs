using Resend;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Email.Services;

namespace CompanyHQ.Email.Extensions;

public static class EmailServiceCollectionExtensions
{
    /// <summary>Registers the domain and outbox with an explicitly unavailable transport.</summary>
    public static IServiceCollection AddUnconfiguredEmail(this IServiceCollection services, IConfiguration configuration) =>
        AddEmailCore<UnconfiguredEmailProvider>(services, configuration, true);
    /// <summary>
    /// Registers the email outbox, its worker, template management, and delivery through Resend.
    /// The host registers the Resend client first (<c>services.AddResend(apiKey)</c> from the
    /// <c>Resend</c> package); without it the host fails its build-time validation.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
        => AddEmailCore<ResendEmailProvider>(services, configuration, false);

    /// <summary>Opt-in Resend registration. Missing credentials register an explicitly unavailable transport.</summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration, bool allowUnconfigured)
    {
        if (!allowUnconfigured) return services.AddEmail(configuration);
        var configured = !string.IsNullOrWhiteSpace(configuration["Resend:ApiKey"])
            && !string.IsNullOrWhiteSpace(configuration["Email:From"]);
        if (!configured) return AddEmailCore<UnconfiguredEmailProvider>(services, configuration, true);
        services.AddResend(configuration["Resend:ApiKey"]!);
        return AddEmailCore<ResendEmailProvider>(services, configuration, false);
    }

    /// <summary>Company-owned transport registration; no vendor client or fallback is supplied.</summary>
    public static IServiceCollection AddEmail<TProvider>(this IServiceCollection services, IConfiguration configuration,
        bool allowUnconfigured = false) where TProvider : class, IEmailProvider =>
        AddEmailCore<TProvider>(services, configuration, allowUnconfigured);

    private static IServiceCollection AddEmailCore<TProvider>(IServiceCollection services, IConfiguration configuration,
        bool allowUnconfigured) where TProvider : class, IEmailProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new();
        if (!allowUnconfigured || !string.IsNullOrWhiteSpace(options.From)) options.Validate();
        else if (options.MaxSendsPerSecond < 1) throw new InvalidOperationException("Email:MaxSendsPerSecond must be at least 1.");

        services.AddSingleton(options);
        services.AddSingleton<EmailSendPacer>();
        services.AddScoped<IEmailProvider, TProvider>();

        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IEmailDeliveryQuery, EmailDeliveryQuery>();
        services.AddScoped<EmailQueueProcessor>();
        services.AddHostedService<EmailWorker>();
        services.AddScoped<IEmailTemplateStore, EmailTemplateStore>();
        services.AddScoped<IEmailTemplateService, EmailTemplateService>();

        // The narrow port every other service sends through; the same scoped instance.
        services.AddScoped<IEmailTemplateSender>(provider => provider.GetRequiredService<IEmailTemplateService>());
        services.AddControllers()
            .AddApplicationPart(typeof(EmailServiceCollectionExtensions).Assembly);

        return services;
    }
}
