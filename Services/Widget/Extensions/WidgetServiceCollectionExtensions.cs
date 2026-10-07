using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Widget.Contracts.Models;
using CompanyHQ.Services.Widget.Services;

namespace CompanyHQ.Services.Widget.Extensions;

/// <summary>Registers the public widget front door and its controller assembly.</summary>
public static class WidgetServiceCollectionExtensions
{
    public const string SubmitRateLimitPolicyName = "widget-submit";
    public const string ChatRateLimitPolicyName = "widget-chat";

    public static IServiceCollection AddWidget(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var options = configuration.GetSection(WidgetOptions.SectionName).Get<WidgetOptions>() ?? new();
        options.Validate();

        services.AddSingleton(options);
        services.AddScoped<IWidgetService, WidgetService>();

        // Every route is anonymous, so the caller's address is the only handle for a brake: few new
        // conversations and reports, but enough follow-ups for a real exchange.
        services.AddRateLimiter(rateLimiter =>
        {
            rateLimiter.AddPolicy(SubmitRateLimitPolicyName, context => PerAddress(context, 5, TimeSpan.FromMinutes(15)));
            rateLimiter.AddPolicy(ChatRateLimitPolicyName, context => PerAddress(context, 60, TimeSpan.FromMinutes(1)));
        });

        services.AddControllers().AddApplicationPart(typeof(WidgetServiceCollectionExtensions).Assembly);

        return services;
    }

    private static RateLimitPartition<string> PerAddress(HttpContext context, int permits, TimeSpan window) =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permits,
                Window = window,
                QueueLimit = 0,
                AutoReplenishment = true
            });
}
