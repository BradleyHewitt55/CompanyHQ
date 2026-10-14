using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

using CompanyHQ.Geo.Contracts.Models;

namespace CompanyHQ.Geo.Extensions;

/// <summary>
/// The per-client inbound limit on <c>/api/geo</c>. A policy class rather than a lambda because it
/// carries its own <see cref="OnRejected"/>: set on the shared <c>RateLimiterOptions</c>, that
/// handler would answer every other service's rejections as a Geo one.
/// </summary>
internal sealed class GeoRateLimitPolicy(GeoOptions options) : IRateLimiterPolicy<string>
{
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, cancellationToken) =>
    {
        var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfterValue)
            ? Math.Max(1, (int)Math.Ceiling(retryAfterValue.TotalSeconds))
            : options.InboundRateLimitWindowSeconds;

        context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString();
        context.HttpContext.Response.ContentType = "application/problem+json";
        await context.HttpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status429TooManyRequests,
                Title = "Too many Geo requests.",
                Detail = "Please wait before requesting another area lookup."
            },
            cancellationToken);
    };

    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            // Do not trust arbitrary X-Forwarded-For input. If a deployment uses a reverse proxy, it
            // must configure ASP.NET's trusted forwarded-header middleware before rate limiting.
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown-client",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.InboundRateLimitPermitLimit,
                Window = TimeSpan.FromSeconds(options.InboundRateLimitWindowSeconds),
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
