using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Media.Contracts.Models;
using CompanyHQ.Media.Services;
using CompanyHQ.Media.Contracts.Ports;
using CompanyHQ.Users.Contracts.Ports;

namespace CompanyHQ.Media.Extensions;

/// <summary>Registers Media's object lifecycle, domain services, and S3 object storage.</summary>
public static class MediaServiceCollectionExtensions
{
    /// <summary>
    /// The host registers its <c>IAmazonS3</c> client first (the <c>AWSSDK.S3</c> package, pointed
    /// at its S3-compatible store); without it the host fails its build-time validation.
    /// </summary>
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = BindOptions(configuration);
        options.Validate();

        services.AddSingleton(options);
        services.AddSingleton<IMediaObjectStorageProvider, S3MediaObjectStorageProvider>();
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
