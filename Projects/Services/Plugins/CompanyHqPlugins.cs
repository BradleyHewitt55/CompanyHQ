using System.Reflection;
using Abstractions;
using CompanyHQ.Services.Auth;
using CompanyHQ.Services.Companies;
using CompanyHQ.Services.Postgress;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Services.Plugins;

// One list, one order, every host. Previously the Admin host, the CLI, and the Blazor
// router each named the plugins separately and could disagree about what was loaded.
public static class CompanyHqPlugins
{
    // Companies first: it owns the paths and tenant context the others resolve against.
    // Postgress before Auth: Auth's user store needs the data provider registered.
    public static IReadOnlyList<ICompanyHqPlugin> Default { get; } =
        [new CompaniesPlugin(), new PostgressPlugin(), new AuthPlugin()];

    public static Assembly[] RouteAssemblies { get; } =
        Default.SelectMany(plugin => plugin.RouteAssemblies).Distinct().ToArray();

    public static IServiceCollection AddCompanyHqPlugins(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        foreach (var plugin in Default)
        {
            plugin.ConfigureServices(services, configuration);
        }

        return services;
    }

    // Call after the company is resolved and before antiforgery and endpoint mapping;
    // plugins assume the ambient company is already set when their middleware runs.
    public static IApplicationBuilder UseCompanyHqPlugins(this IApplicationBuilder application)
    {
        foreach (var plugin in Default)
        {
            plugin.ConfigureApplication(application);
        }

        return application;
    }
}
