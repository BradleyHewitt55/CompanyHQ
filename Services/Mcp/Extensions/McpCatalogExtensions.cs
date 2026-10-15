using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Mcp.Extensions;

public static class McpCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddMcpServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = null, Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "mcp", "MCP", "Let AI assistants use your services.",
            "Lets ChatGPT, Claude and other AI assistants connect with an agent key or OAuth sign-in. Each assistant only sees the tools its role allows; staff assistants can see product costs, customer assistants cannot. Turning it off disconnects every assistant.",
            Core: false, Dependencies: [],
            Providers: null,
            DefaultProvider: null, MenuPath: null, Capabilities: null),
            typeof(McpCatalogExtensions).Assembly, loadedProvider,
            isConfigured));
}
