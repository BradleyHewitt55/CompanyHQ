using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CompanyHQ.Mcp.Contracts.Forms;

public sealed record RegisterMcpClientRequest(
    [property: JsonPropertyName("redirect_uris")]
    [param: Required, MinLength(1)] IReadOnlyList<string> RedirectUris,
    [property: JsonPropertyName("client_name")] string? ClientName = null,
    [property: JsonPropertyName("grant_types")] IReadOnlyList<string>? GrantTypes = null,
    [property: JsonPropertyName("response_types")] IReadOnlyList<string>? ResponseTypes = null,
    [property: JsonPropertyName("token_endpoint_auth_method")] string? TokenEndpointAuthMethod = null,
    [property: JsonPropertyName("scope")] string? Scope = null);
