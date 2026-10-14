namespace CompanyHQ.Auth.Contracts.Identity;

/// <summary>
/// Named authorization policies <c>AddAuth</c> registers for other services to apply. Auth owns the
/// policy; it does not own the decision to apply it to any particular endpoint.
/// </summary>
public static class AuthPolicies
{
    /// <summary>
    /// Gates the outbound-to-strangers surfaces — sending a message — on a confirmed email address. Everything else an account can do stays open to an unverified caller.
    /// </summary>
    public const string RequireVerifiedEmail = "RequireVerifiedEmail";
}
