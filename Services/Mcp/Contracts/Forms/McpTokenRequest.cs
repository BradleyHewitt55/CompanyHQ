using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Mcp.Contracts.Forms;

public sealed record McpTokenRequest(
    [Required] string GrantType,
    [Required, MaxLength(2048)] string ClientId,
    [Required, MaxLength(4096)] string Code,
    [Required, MaxLength(2048)] string RedirectUri,
    [Required, MaxLength(256)] string CodeVerifier,
    [Required, MaxLength(2048)] string Resource);
