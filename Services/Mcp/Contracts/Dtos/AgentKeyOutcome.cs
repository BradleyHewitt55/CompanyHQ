using CompanyHQ.Services.Mcp.Contracts.Enums;

namespace CompanyHQ.Services.Mcp.Contracts.Dtos;

/// <summary>A rejected create is an expected outcome, not an exception.</summary>
public readonly record struct AgentKeyOutcome(IssuedAgentKeyDto? Key, AgentKeyError Error)
{
    public bool Succeeded => Key is not null;

    public static AgentKeyOutcome Success(IssuedAgentKeyDto key) => new(key, AgentKeyError.None);

    public static AgentKeyOutcome Failure(AgentKeyError error) => new(null, error);
}
