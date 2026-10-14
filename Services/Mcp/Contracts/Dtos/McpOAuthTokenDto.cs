using System.Text.Json.Serialization;

namespace CompanyHQ.Mcp.Contracts.Dtos;

public sealed record McpOAuthTokenDto(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("resource")] string Resource,
    [property: JsonPropertyName("refresh_token")]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? RefreshToken = null);
