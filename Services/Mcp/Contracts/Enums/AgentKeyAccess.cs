using System.Text.Json.Serialization;

using CompanyHQ.Services.Postgres.Contracts.Serialization;

namespace CompanyHQ.Services.Mcp.Contracts.Enums;

/// <summary>
/// A ceiling, not a grant — effective capability is <c>min(UserRole, AgentKeyAccess)</c>. <c>ReadOnly</c>
/// is <c>0</c> for the same reason <c>ResponseAudience.Client</c> is: a forgotten argument under-grants
/// and can never over-grant.
/// </summary>
[JsonConverter(typeof(StrictStringEnumConverter<AgentKeyAccess>))]
public enum AgentKeyAccess
{
    ReadOnly = 0,
    Write,
    Full
}
