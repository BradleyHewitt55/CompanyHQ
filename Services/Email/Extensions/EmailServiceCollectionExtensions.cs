using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Email.Services;

namespace CompanyHQ.Email.Extensions;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers the email outbox, its worker, template management, and delivery through Resend.
    /// The host registers the Resend client first (<c>services.AddResend(apiKey)</c> from the
    /// <c>Resend</c> package); without it the host fails its build-time validation.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(EmailOptions.SectionName).Get<EmailOptions>() ?? new();
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<EmailSendPacer>();
        services.AddScoped<IEmailProvider, ResendEmailProvider>();

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
