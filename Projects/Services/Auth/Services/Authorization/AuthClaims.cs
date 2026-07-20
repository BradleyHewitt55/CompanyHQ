namespace CompanyHQ.Services.Auth.Services.Authorization;

public static class AuthClaims
{
    public const string Company = "company";
    public const string CurrentCompanyPolicy = "CurrentCompany";
    public const string CompanyAdminPolicy = "CompanyAdmin";
    public const string CredentialRateLimitPolicy = "AuthCredentials";
}
