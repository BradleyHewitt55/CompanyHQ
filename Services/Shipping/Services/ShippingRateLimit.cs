using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;

namespace CompanyHQ.Services.Shipping.Services;

/// <summary>
/// The budget for the anonymous quote. A quote reads every active method and may geocode, so the
/// remote address is braked; it is a cost control, not an authorization control.
/// </summary>
public static class ShippingRateLimit
{
    public const string QuotePolicyName = "shipping-quote";

    internal static void AddPolicy(RateLimiterOptions options) =>
        options.AddPolicy(QuotePolicyName, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown-origin",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 60,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
}
