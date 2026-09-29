using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Email.Services;

namespace CompanyHQ.Services.Email.Extensions;

public static class EmailServiceCollectionExtensions
{
    /// <summary>
    /// Registers the email outbox, its worker, and template management. A provider adapter (Resend)
    /// supplies <see cref="IEmailProvider"/>; without one the host fails its build-time validation.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IEmailService, EmailService>();
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
