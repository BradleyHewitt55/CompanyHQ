using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Cloudflare.Contracts.Models;
using CompanyHQ.Services.Cloudflare.Services;
using CompanyHQ.Services.Media.Contracts.Ports;

namespace CompanyHQ.Services.Cloudflare.Extensions;

/// <summary>Registers Cloudflare R2 behind Media's object-storage port.</summary>
public static class CloudflareServiceCollectionExtensions
{
    public static IServiceCollection AddCloudflare(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var r2 = configuration.GetSection(R2Options.SectionName).Get<R2Options>() ?? new();
        r2.Validate();

        services.AddSingleton(r2);
        services.AddSingleton<IMediaObjectStorageProvider, CloudflareR2ObjectStorageProvider>();
        return services;
    }
}
