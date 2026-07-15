using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Services.Companies.Contracts.Services;

public interface IServiceModule
{
    ServiceManifest Manifest { get; }
    void Configure(IServiceCollection services);
    void MapEndpoints(IEndpointRouteBuilder endpoints);
    IReadOnlyList<ServiceNavigationItem> NavigationItems => [];
    IReadOnlyList<ServiceComponentRegistration> Components => [];
    IReadOnlyList<ServiceSettingsPage> SettingsPages => [];
}

public sealed record ServiceNavigationItem(string Label, string Path, string Icon);
public sealed record ServiceComponentRegistration(string Name, Type ComponentType);
public sealed record ServiceSettingsPage(string Name, Type ComponentType);
