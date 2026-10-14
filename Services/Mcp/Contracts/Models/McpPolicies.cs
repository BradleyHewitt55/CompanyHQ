namespace CompanyHQ.Mcp.Contracts.Models;

/// <summary>Authorization policies <c>AddMcp</c> registers.</summary>
public static class McpPolicies
{
    /// <summary>
    /// Requires the agent-key authentication scheme specifically and the <see cref="McpClaims.Agent"/>
    /// claim it stamps — a human's JWT never carries that claim and is never even evaluated against
    /// this scheme, so it fails here rather than being explicitly refused.
    /// </summary>
    public const string Agent = "McpAgent";
}
