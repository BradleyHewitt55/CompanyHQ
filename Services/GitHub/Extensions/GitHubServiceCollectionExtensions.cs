using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.GitHub.Contracts.Models;
using CompanyHQ.Services.GitHub.Services;

namespace CompanyHQ.Services.GitHub.Extensions;

/// <summary>Registers the GitHub repository connection, its issues, and the controller assembly.</summary>
public static class GitHubServiceCollectionExtensions
{
    public static IServiceCollection AddGitHub(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(GitHubOptions.SectionName).Get<GitHubOptions>() ?? new();
        options.Validate();

        services.AddSingleton(options);
        services.AddHttpClient(GitHubService.HttpClientName);
        services.AddScoped<IGitHubService, GitHubService>();

        services.AddControllers().AddApplicationPart(typeof(GitHubServiceCollectionExtensions).Assembly);

        return services;
    }
}
