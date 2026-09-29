namespace CompanyHQ.Services.Mcp.Contracts.Models;

/// <summary>Claims an agent-key principal carries that a human's JWT never does.</summary>
public static class McpClaims
{
    /// <summary>Present and "true" only on a principal built from an agent key.</summary>
    public const string Agent = "agent";

    /// <summary>The agent key's own id — never the user id, which is already <c>sub</c>.</summary>
    public const string AgentKeyId = "agent_key";

    /// <summary>
    /// The key's <c>AgentKeyAccess</c> ceiling, as text. A ceiling, not a grant — a tool still checks
    /// <c>min(role, this)</c>, never this alone.
    /// </summary>
    public const string AgentKeyAccess = "agent_key_access";

    /// <summary>Repeated claims for tools explicitly disabled on the account or key.</summary>
    public const string DisabledCapability = "mcp_disabled_capability";
}
