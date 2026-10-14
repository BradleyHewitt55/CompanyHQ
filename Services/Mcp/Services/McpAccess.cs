using System.Security.Claims;

using ModelContextProtocol;

using CompanyHQ.Mcp.Contracts.Enums;
using CompanyHQ.Mcp.Contracts.Models;
using CompanyHQ.Postgres.Contracts;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Mcp.Services;

/// <summary>
/// Shared MCP access gates. One case per role, not one helper per tool class.
/// </summary>
public static class McpAccess
{
    private static IReadOnlyDictionary<string, McpActionAttribute> actions =
        new Dictionary<string, McpActionAttribute>(StringComparer.Ordinal);

    public static void Configure(IReadOnlyDictionary<string, McpActionAttribute> discovered) =>
        actions = discovered;

    /// <summary>
    /// Resolves a capability as the intersection of backend role access and explicit deny lists.
    /// Unknown names fail closed so adding a tool requires adding it to the authoritative catalogue.
    /// </summary>
    public static bool IsAllowed(ClaimsPrincipal? principal, string capability)
    {
        if (principal is null)
        {
            return false;
        }

        if (!actions.TryGetValue(capability, out var action)
            || !principal.HasClaim(AuthClaims.Permission, action.Permission))
        {
            return false;
        }

        if (McpCapabilityCatalog.Write.Contains(capability) && !HasWriteAccess(principal))
        {
            return false;
        }

        return !principal.Claims.Any(claim =>
            claim.Type == McpClaims.DisabledCapability
            && string.Equals(claim.Value, capability, StringComparison.Ordinal));
    }

    public static void RequireCapability(ClaimsPrincipal? principal, string capability)
    {
        if (!IsAllowed(principal, capability))
        {
            throw new McpProtocolException(
                $"The '{capability}' capability is not enabled for this account.",
                McpErrorCode.InvalidRequest);
        }
    }

    public static bool HasWriteAccess(ClaimsPrincipal? principal)
    {
        var access = principal?.FindFirstValue(McpClaims.AgentKeyAccess);
        return Enum.TryParse<AgentKeyAccess>(access, out var parsed)
            && parsed is AgentKeyAccess.Write or AgentKeyAccess.Full;
    }
}
