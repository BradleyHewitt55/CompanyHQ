using System.ComponentModel.DataAnnotations;

namespace CompanyHQ.Users.Contracts.Forms;

/// <summary>
/// Body for <c>PUT /api/users/me</c>. Deliberately not <see cref="UpdateUserRequest"/>: this one is
/// the only user write a non-administrator can reach, so it must not carry a role, an email or a
/// password field that a future edit could start honouring.
/// </summary>
/// <remarks>
/// Every field is replaced rather than patched, because the profile form always submits each one.
/// An empty bio or image URL clears it. Both names are required: together they are the public identity
/// every other screen shows and the slug <c>GET /api/users/{slug}</c> resolves.
/// </remarks>
public sealed record UpdateProfileRequest(
    [Required, MaxLength(60), RegularExpression(@".*\S.*")] string FirstName,
    [Required, MaxLength(60), RegularExpression(@".*\S.*")] string LastName,
    [MaxLength(500)] string? Bio = null,
    [MaxLength(2048)] string? ProfileImageUrl = null) : IValidatableObject
{
    public IEnumerable<ValidationResult> Validate(ValidationContext context) =>
        UserProfileValidation.ValidateProfileImageUrl(ProfileImageUrl);
}
