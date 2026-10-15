using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Media.Extensions;

public static class MediaCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddMediaServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "s3", Func<IServiceProvider, bool>? isConfigured = null)
    {
        var providerType = services.LastOrDefault(item => item.ServiceType == typeof(CompanyHQ.Media.Contracts.Ports.IMediaObjectStorageProvider))?.ImplementationType;
        return
        services.AddServiceDescriptor(new(new(
            "media", "Media", "Store images and files.",
            "Stores uploaded images and files, such as product photos, blog covers and logos, and serves them to your site. Always on. The storage provider cannot be changed once files exist, because existing files would be lost.",
            Core: true, Dependencies: [],
            Providers: [
                new("s3", "S3-compatible storage", "Object storage through the official AWS S3 SDK.", [
                    new("Media:Bucket", "Storage bucket.", Secret: false, Required: true, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation."),
                    new("Cloudflare:R2:AccountId", "R2 account identifier when using R2.", Secret: false, Required: false, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation."),
                    new("Cloudflare:R2:AccessKeyId", "R2 access key identifier.", Secret: true, Required: false, ReadOnly: false),
                    new("Cloudflare:R2:SecretAccessKey", "R2 access secret.", Secret: true, Required: false, ReadOnly: false)
                ]),
                new("local", "Local storage", "Company-owned local object storage; requires an explicit provider and migration.", [
                    new("MediaStorage:Root", "Company-owned storage root.", Secret: false, Required: true, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation."),
                    new("MediaStorage:SigningKey", "Signs access to stored media.", Secret: true, Required: true, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation.")
                ])
            ],
            DefaultProvider: "s3", MenuPath: "/media", Capabilities: null, ReadPermission: "media.manage", ProviderChangeRequiresMigration: true,
            ConfigurationKeys: [
                new("Media:ContentBaseUrl", "API origin for durable media content URLs when no public domain is configured.", Required: false, ReadOnly: true, ProtectionReason: "Changing this invalidates stored durable URLs."),
                new("Media:PublicBaseUrl", "Public Base URL.", Required: false, ReadOnly: true, ProtectionReason: "Changing this can invalidate issued credentials or stored durable URLs."),
                new("Media:MaxUploadBytes", "Max Upload Bytes.", Required: false),
                new("Media:MaxVideoUploadBytes", "Max Video Upload Bytes.", Required: false),
                new("Media:AllowedContentTypes", "Allowed Content Types.", Required: false),
                new("Media:UploadUrlLifetime", "Upload URL Lifetime.", Required: false),
                new("Media:DownloadUrlLifetime", "Download URL Lifetime.", Required: false),
                new("Media:PendingRetention", "Pending Retention.", Required: false),
                new("Media:CleanupInterval", "Cleanup Interval.", Required: false),
                new("Media:CleanupBatchSize", "Cleanup Batch Size.", Required: false),
                new("Media:CleanupMaxAttempts", "Cleanup Max Attempts.", Required: false)
            ]),
            typeof(MediaCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (provider => !string.IsNullOrWhiteSpace(provider.GetRequiredService<CompanyHQ.Media.Contracts.Models.MediaOptions>().Bucket)
                && providerType is { } storage
                && storage != typeof(CompanyHQ.Media.Services.UnconfiguredMediaObjectStorageProvider))));
    }
}
