using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Auth.Contracts.Forms;

/// <summary>
/// Body for <c>POST /api/auth/login</c>.
/// </summary>
/// <remarks>
/// The password has no length rules here on purpose: rejecting a short password at login tells a
/// caller their guess did not even meet the policy. Validation belongs on registration.
/// <paramref name="DeviceLabel"/> is free text ("Pixel 8", "Web") and is sanitized before storage —
/// never trusted as-is.
/// </remarks>
public sealed record LoginRequest(
    [Required, EmailAddress, MaxLength(320)] string Email,
    [Required] string Password,
    string? DeviceLabel = null);
