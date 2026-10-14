namespace CompanyHQ.Mcp.Contracts.Forms;

/// <summary>Null restores inheritance from the connection owner's role.</summary>
public sealed record AssignAgentRoleRequest(Guid? AccessRoleId);
