using System.Security.Claims;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Moderation.Contracts.Ports;
using CompanyHQ.Moderation.Services;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Moderation.Extensions;

/// <summary>Registers the moderation service and its controller assembly.</summary>
public static class ModerationServiceCollectionExtensions
{
    public const string ReportRateLimitPolicyName = "ModerationReports";

    /// <summary>Registers moderation after the shared Postgres, User, and Cache services.</summary>
    public static IServiceCollection AddModeration(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IContentHider, ContentHider>();
        services.AddScoped<IBlockService, BlockService>();
        services.AddScoped<IContentRestrictionService, ContentRestrictionService>();
        services.AddScoped<IReportTargetRegistry, ReportTargetRegistry>();
        services.AddScoped<IReportableContentSource, UserReportableContentSource>();
        services.AddScoped<IContentReportService, ContentReportService>();
        services.AddScoped<IAccountFlagService, AccountFlagService>();

        // The narrow ports other services consume; the same scoped instances.
        services.AddScoped<IBlockChecker>(provider => provider.GetRequiredService<IBlockService>());
        services.AddScoped<IAccountFlags>(provider => provider.GetRequiredService<IAccountFlagService>());
        services.AddScoped<IContentRestrictions>(provider => provider.GetRequiredService<IContentRestrictionService>());

        services.AddRateLimiter(options =>
        {
            // Reports are limited per authenticated account, not by IP: a shared household IP
            // must not make one person's report budget depend on another person's activity.
            options.AddPolicy(
                ReportRateLimitPolicyName,
                context => RateLimitPartition.GetFixedWindowLimiter(
                    context.User.GetUserId()?.ToString() ?? "unknown-user",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromHours(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });

        // Controllers ship with the service so the API host only composes assemblies and middleware.
        services.AddControllers().AddApplicationPart(typeof(ModerationServiceCollectionExtensions).Assembly);

        return services;
    }
}
