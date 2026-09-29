using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Services.Mcp.Contracts.Forms;

public sealed record McpAuthorizeRequest(
    [Required] string ResponseType,
    [Required, MaxLength(2048)] string ClientId,
    [Required, MaxLength(2048)] string RedirectUri,
    [Required, MaxLength(128)] string CodeChallenge,
    [Required] string CodeChallengeMethod,
    [Required, MaxLength(2048)] string Resource,
    string? Scope = null,
    string? State = null);
