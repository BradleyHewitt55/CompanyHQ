using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.GitHub.Extensions;

public static class GitHubCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddGitHubServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "github", Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "github", "GitHub", "Send bug reports to your GitHub repository.",
            "Connect a GitHub repository so issues reported from your site or widget are created there. Needs an encryption key in Environment to store the repository token safely.",
            Core: false, Dependencies: [],
            Providers: [
                new("github", "GitHub", "Encrypted company repository connection.", [
                    new("GitHub:TokenEncryptionKey", "Encryption key for stored repository tokens.", Secret: true, Required: true, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation.")
                ])
            ],
            DefaultProvider: "github", MenuPath: "/settings/github", Capabilities: null, ReadPermission: "github.manage"),
            typeof(GitHubCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (provider => provider.GetRequiredService<CompanyHQ.GitHub.Contracts.Models.GitHubOptions>().IsConfigured)));
}
