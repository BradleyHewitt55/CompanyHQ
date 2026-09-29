using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Mcp.Contracts.Forms;

public sealed record McpRefreshTokenRequest(
    [Required] string GrantType,
    [Required, MaxLength(2048)] string ClientId,
    [Required, MaxLength(4096)] string RefreshToken,
    [Required, MaxLength(2048)] string Resource);
