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
    /// <summary>Registers the lifecycle with an explicitly unavailable object store.</summary>
    public static IServiceCollection AddUnconfiguredMedia(this IServiceCollection services, IConfiguration configuration) =>
        AddMediaCore<UnconfiguredMediaObjectStorageProvider>(services, configuration, true);
    /// <summary>
    /// The host registers its <c>IAmazonS3</c> client first (the <c>AWSSDK.S3</c> package, pointed
    /// at its S3-compatible store); without it the host fails its build-time validation.
    /// </summary>
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration)
        => AddMediaCore<S3MediaObjectStorageProvider>(services, configuration, false);

    /// <summary>Opt-in boot without S3 credentials. The unavailable provider never fabricates storage success.</summary>
    public static IServiceCollection AddMedia(this IServiceCollection services, IConfiguration configuration, bool allowUnconfigured) =>
        !allowUnconfigured ? services.AddMedia(configuration) : services.Any(item => item.ServiceType == typeof(Amazon.S3.IAmazonS3))
            ? AddMediaCore<S3MediaObjectStorageProvider>(services, configuration, false)
            : AddMediaCore<UnconfiguredMediaObjectStorageProvider>(services, configuration, true);

    /// <summary>Registers an explicit company-owned storage provider.</summary>
    public static IServiceCollection AddMedia<TStorage>(this IServiceCollection services, IConfiguration configuration,
        bool allowUnconfigured = false) where TStorage : class, IMediaObjectStorageProvider =>
        AddMediaCore<TStorage>(services, configuration, allowUnconfigured);

    private static IServiceCollection AddMediaCore<TStorage>(IServiceCollection services, IConfiguration configuration,
        bool allowUnconfigured) where TStorage : class, IMediaObjectStorageProvider
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = BindOptions(configuration);
        options.Validate(allowUnconfigured);

        services.AddSingleton(options);
        services.AddSingleton<IMediaObjectStorageProvider, TStorage>();
        services.AddScoped<IMediaAccessPolicyResolver, MediaAccessPolicyResolver>();
        services.AddScoped<MediaObjectLifecycleStore>();
        services.AddScoped<IMediaObjectLifecycleStore>(provider => provider.GetRequiredService<MediaObjectLifecycleStore>());
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
