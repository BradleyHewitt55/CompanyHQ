using Microsoft.AspNetCore.Authentication;

namespace CompanyHQ.Mcp.Authentication;

/// <summary>No options of its own — the scheme name is enough to select it, and everything else is fixed.</summary>
public sealed class AgentKeyAuthenticationSchemeOptions : AuthenticationSchemeOptions;
