using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using ModelContextProtocol.AspNetCore;

using CompanyHQ.Mcp.Contracts.Models;

namespace CompanyHQ.Mcp.Extensions;

public static class McpEndpointRouteBuilderExtensions
{
    /// <summary>
    /// Maps the MCP endpoint behind the agent policy. Outside <c>MapControllers</c> on purpose: MCP's
    /// own discovery is the contract, and a controller-based OpenAPI scan or an unauthenticated
    /// <c>/health</c> probe describing this endpoint would send a client down the wrong path.
    /// </summary>
    public static IEndpointRouteBuilder MapMcpEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapMcp("/mcp").RequireAuthorization(McpPolicies.Agent);

        return endpoints;
    }
}
