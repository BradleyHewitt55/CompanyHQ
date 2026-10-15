using CompanyHQ.ServiceCatalog;
using CompanyHQ.ServiceCatalog.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyHQ.Auth.Extensions;

public static class AuthCatalogExtensions
{
    /// <summary>Registers public metadata. The host explicitly allowlists this service in AddServiceCatalog.</summary>
    public static IServiceCollection AddAuthServiceDescriptor(this IServiceCollection services,
        string? loadedProvider = "jwt", Func<IServiceProvider, bool>? isConfigured = null) =>
        services.AddServiceDescriptor(new(new(
            "auth", "Authentication", "Sign-in, registration and account security.",
            "Handles sign-in, registration, email verification, password resets and session tokens. Always on, because nobody could sign in to manage the company without it.",
            Core: true, Dependencies: [],
            Providers: [
                new("jwt", "JWT", "Company-issued signed access tokens.", [
                    new("Jwt:SigningKey", "Access-token signing key.", Secret: true, Required: true, ReadOnly: true, ProtectionReason: "Requires coordinated migration or credential rotation."),
                    new("Auth:WebBaseAddress", "Company login and recovery origin.", Secret: false, Required: true, ReadOnly: false)
                ])
            ],
            DefaultProvider: "jwt", MenuPath: null, Capabilities: null,
            ConfigurationKeys: [
                new("Jwt:Issuer", "Issuer.", Required: false, ReadOnly: true, ProtectionReason: "Changing this can invalidate issued credentials or stored durable URLs."),
                new("Jwt:Audience", "Audience.", Required: false, ReadOnly: true, ProtectionReason: "Changing this can invalidate issued credentials or stored durable URLs."),
                new("Jwt:AccessTokenMinutes", "Access Token Minutes.", Required: false),
                new("Jwt:RefreshTokenDays", "Refresh Token Days.", Required: false),
                new("Jwt:ClockSkewSeconds", "Clock Skew Seconds.", Required: false),
                new("Auth:PasswordResetMinutes", "Password Reset Minutes.", Required: false),
                new("Auth:EmailVerificationHours", "Email Verification Hours.", Required: false),
                new("Auth:EmailChangeMinutes", "Email Change Minutes.", Required: false),
                new("Auth:AccountDeletionMinutes", "Account Deletion Minutes.", Required: false),
                new("Auth:LoginRateLimit:MaxAttempts", "Max Attempts.", Required: false),
                new("Auth:LoginRateLimit:WindowSeconds", "Window Seconds.", Required: false),
                new("Auth:RefreshTokenCleanup:IntervalMinutes", "Interval Minutes.", Required: false),
                new("Auth:RefreshTokenCleanup:BatchSize", "Batch Size.", Required: false),
                new("Auth:RefreshTokenCleanup:RevokedRetentionDays", "Revoked Retention Days.", Required: false)
            ]),
            typeof(AuthCatalogExtensions).Assembly, loadedProvider,
            isConfigured ?? (provider => !string.IsNullOrWhiteSpace(provider.GetRequiredService<CompanyHQ.Auth.Contracts.Models.JwtOptions>().SigningKey))));
}
