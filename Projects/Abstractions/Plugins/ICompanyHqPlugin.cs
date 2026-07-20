using System.Reflection;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Abstractions.Plugins;

public interface ICompanyHqPlugin
{
    PluginDescriptor Descriptor { get; }

    void ConfigureServices(IServiceCollection services, IConfiguration configuration);

    // Middleware remains owned by the plugin so hosts cannot accidentally register
    // a service without the pipeline behavior that makes it safe.
    void ConfigureApplication(IApplicationBuilder application)
    {
    }

    IEnumerable<Assembly> RouteAssemblies => [];
}
