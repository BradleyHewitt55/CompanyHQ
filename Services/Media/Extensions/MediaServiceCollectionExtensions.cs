using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Media.Contracts.Models;
using CompanyHQ.Services.Media.Services;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Users.Contracts.Ports;

namespace CompanyHQ.Services.Media.Extensions;

/// <summary>Registers Media's provider-neutral object lifecycle and domain services.</summary>
public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = BindOptions(configuration);
        options.Validate();

        services.AddSingleton(options);
        services.AddScoped<IMediaAccessPolicyResolver, MediaAccessPolicyResolver>();
        services.AddScoped<IMediaObjectLifecycleStore, MediaObjectLifecycleStore>();
        services.AddScoped<IProfileMediaLifecycle, ProfileMediaLifecycle>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IMediaAttachmentVerifier, MediaAttachmentVerifier>();
        services.AddScoped<MediaCleanupProcessor>();
        services.AddHostedService<MediaCleanupWorker>();
        services.AddControllers().AddApplicationPart(typeof(MediaServiceCollectionExtensions).Assembly);

        return services;
    }

    private static MediaOptions BindOptions(IConfiguration configuration)
    {
        var options = configuration.GetSection(MediaOptions.SectionName).Get<MediaOptions>() ?? new();

        // The S3 API host signs every GET, so an img pointed at it is always a 400. Treat it as "no
        // public domain configured" and serve through this API rather than storing dead URLs.
        if (options.HasPublicDomain
            && Uri.TryCreate(options.PublicBaseUrl, UriKind.Absolute, out var publicUri)
            && publicUri.Host.EndsWith(".r2.cloudflarestorage.com", StringComparison.OrdinalIgnoreCase))
        {
            options.PublicBaseUrl = string.Empty;
        }

        return options;
    }
}
