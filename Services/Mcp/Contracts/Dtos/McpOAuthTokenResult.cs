namespace CompanyHQ.Mcp.Contracts.Dtos;

public readonly record struct McpOAuthTokenResult(McpOAuthTokenDto? Token, string? Error)
{
    public bool Succeeded => Token is not null;

    public static McpOAuthTokenResult Success(McpOAuthTokenDto token) => new(token, null);

    public static McpOAuthTokenResult Failure(string error) => new(null, error);
}
