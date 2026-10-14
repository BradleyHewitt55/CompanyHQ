using CompanyHQ.Mcp.Contracts.Dtos;
using CompanyHQ.Mcp.Contracts.Forms;

namespace CompanyHQ.Mcp.Services;

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
