using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abstractions;

public interface ICompanyHqPlugin
{
    string Id { get; }
    string Name { get; }
    Version Version { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    // A plugin that registers middleware-backed services (auth, rate limiting) has to
    // own the middleware too, or every host has to remember the plugin's private
    // pipeline requirements and they drift apart. Hosts call this at one documented
    // slot: after the company is resolved, before antiforgery and endpoint mapping.
    // Non-web hosts such as the CLI never call it, which is why it defaults to no-op.
    void ConfigureApplication(IApplicationBuilder application)
    {
    }

    // Assemblies carrying routable components. Hosts feed these to both the Blazor
    // router and AddAdditionalAssemblies so a plugin's pages resolve without the
    // host naming the plugin's types.
    IEnumerable<Assembly> RouteAssemblies => [];
}
