using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Resend.Contracts.Models;
using CompanyHQ.Services.Resend.Services;

namespace CompanyHQ.Services.Resend.Extensions;

public static class ResendServiceCollectionExtensions
{
    public static IServiceCollection AddResend(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(ResendOptions.SectionName).Get<ResendOptions>() ?? new();
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<ResendRateLimiter>();
        services.AddHttpClient<IEmailProvider, ResendEmailProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
            client.Timeout = TimeSpan.FromSeconds(20);
        });

        return services;
    }
}
