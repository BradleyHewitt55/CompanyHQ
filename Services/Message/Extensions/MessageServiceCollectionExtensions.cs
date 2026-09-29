using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Messages.Hubs;
using CompanyHQ.Services.Messages.Services;
using CompanyHQ.Services.Messages.Contracts.Models;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Messages.Extensions;

/// <summary>Registers the message service and the controller assembly.</summary>
public static class MessageServiceCollectionExtensions
{
    public const string SendRateLimitPolicyName = "MessageSends";

    /// <summary>Registers message persistence and HTTP endpoints after its shared dependencies.</summary>
    public static IServiceCollection AddMessages(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var spamOptions = configuration.GetSection(MessageSpamOptions.SectionName).Get<MessageSpamOptions>() ?? new();
        spamOptions.Validate();

        services.AddControllers()
            .AddApplicationPart(typeof(MessageServiceCollectionExtensions).Assembly);

        services.AddScoped<IMessageService, MessageService>();
        services.AddSignalR();
        services.AddScoped<IMessageBroadcaster, SignalRMessageBroadcaster>();
        services.AddSingleton(spamOptions);
        services.AddScoped<MessageSpamDetector>();
        // Public so the host can bridge them to a reporting or media service without Message
        // referencing either.
        services.AddScoped<MessageContextReader>();
        services.AddScoped<MessageHistoryClearer>();
        services.AddScoped<MessageAttachmentAccess>();

        // Per account, not per address: sends are authenticated, and a shared household address
        // must not make one member's budget depend on another's.
        services.AddRateLimiter(rateLimiter => rateLimiter.AddPolicy(
            SendRateLimitPolicyName,
            context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(AuthClaims.Subject)?.Value ?? "unknown-user",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                })));

        return services;
    }
}
