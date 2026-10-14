using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

/// <summary>
/// <see cref="RefreshToken"/> identifies which of the caller's sessions to keep alive (rotated) while
/// every other one is revoked — an access token alone carries no link back to a specific session.
/// </summary>
public sealed record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(12), MaxLength(256)] string NewPassword,
    [Required] string RefreshToken);
