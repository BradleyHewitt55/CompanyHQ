using CompanyHQ.Services.Mcp.Contracts.Dtos;
using CompanyHQ.Services.Mcp.Contracts.Forms;

namespace CompanyHQ.Services.Mcp.Services;

public interface IMcpOAuthService
{
    Task<McpOAuthClientDto> RegisterClientAsync(
        RegisterMcpClientRequest request,
        CancellationToken cancellationToken = default);

    Task<string?> IssueAuthorizationCodeAsync(
        Guid userId,
        McpAuthorizeRequest request,
        CancellationToken cancellationToken = default);

    Task<McpOAuthTokenResult> RedeemAuthorizationCodeAsync(
        McpTokenRequest request,
        CancellationToken cancellationToken = default);

    Task<McpOAuthTokenResult> RedeemRefreshTokenAsync(
        McpRefreshTokenRequest request,
        CancellationToken cancellationToken = default);
}
