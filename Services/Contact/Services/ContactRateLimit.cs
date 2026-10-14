using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace CompanyHQ.Contact.Services;

/// <summary>
/// The submission budget for the anonymous contact form. There is no account to partition by, so the
/// remote address is the only handle available — it is a spam brake, not an authorization control.
/// </summary>
public static class ContactRateLimit
{
    public const string PolicyName = "contact-submit";

    internal static void AddPolicy(RateLimiterOptions options) =>
        options.AddPolicy(PolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown-origin",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(15),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
}
