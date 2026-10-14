namespace CompanyHQ.Auth.Contracts.Identity;

/// <summary>
/// The claim names CompanyHQ tokens carry. Short, unmapped names — <c>MapInboundClaims</c> is off, so
/// what is written here is exactly what a client and the API both read back.
/// </summary>
public static class AuthClaims
{
    public const string Subject = "sub";

    public const string Email = "email";

    public const string FirstName = "given_name";

    public const string LastName = "family_name";

    public const string Role = "role";
    public const string Permission = "permission";

    public const string EmailVerified = "email_verified";
}
