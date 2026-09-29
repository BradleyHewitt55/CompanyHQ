using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using CompanyHQ.Services.Mcp.Authentication;
using CompanyHQ.Services.Mcp.Contracts.Models;
using CompanyHQ.Services.Mcp.Services;

namespace CompanyHQ.Services.Mcp.Extensions;

public static class McpServiceCollectionExtensions
{
    /// <summary>
    /// Registers agent-key lifecycle, the agent-key authentication scheme, and the controller that
    /// manages keys. Call after <c>AddUsers</c> and <c>AddAuth</c> — the scheme reads the account row
    /// AddUsers' model exposes, and needs the bearer scheme AddAuth already registered as the host
    /// default to exist first (this adds a second, non-default scheme alongside it).
    /// </summary>
    public static IServiceCollection AddMcp(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var oauthOptions = configuration.GetSection(McpOAuthOptions.SectionName).Get<McpOAuthOptions>() ?? new();

        // One public web address serves both sign-in links and the OAuth consent page, so an
        // unset MCP value follows Auth's rather than being configured twice.
        if (string.IsNullOrWhiteSpace(oauthOptions.PublicWebBaseAddress)
            && configuration["Auth:WebBaseAddress"] is { Length: > 0 } webBaseAddress)
        {
            oauthOptions.PublicWebBaseAddress = webBaseAddress;
        }

        oauthOptions.Validate();
        services.AddSingleton(oauthOptions);

        services.AddScoped<IAgentKeyService, AgentKeyService>();
        services.AddScoped<IMcpOAuthService, McpOAuthService>();

        // A second, non-default scheme. Nothing outside /mcp names it, so nothing outside /mcp ever
        // evaluates a request against it — an agent key presented elsewhere is simply never checked
        // here, and the host's existing default (JWT) scheme handles that request as it always did.
        services.AddAuthentication()
            .AddScheme<AgentKeyAuthenticationSchemeOptions, AgentKeyAuthenticationHandler>(
                AgentKeyAuthenticationHandler.SchemeName,
                configureOptions: null);

        services.AddAuthorization(options =>
        {
            options.AddPolicy(McpPolicies.Agent, policy => policy
                .AddAuthenticationSchemes(AgentKeyAuthenticationHandler.SchemeName)
                .RequireAuthenticatedUser()
                .RequireClaim(McpClaims.Agent));
        });

        // Controllers live in this assembly, so the host has to be told to look in it.
        var mvc = services.AddControllers().AddApplicationPart(typeof(McpServiceCollectionExtensions).Assembly);

        // Tools read the calling principal through this rather than a controller's ambient User.
        services.AddHttpContextAccessor();

        // Stateless: a session-stateful endpoint needs sticky routing the moment the API runs more
        // than one instance, and discovering that in production is worse than not having sessions.
        var actions = McpActionRegistry.Discover(mvc.PartManager);
        McpAccess.Configure(actions);
        services.AddMcpServer(options => options.ServerInfo = new()
        {
            Name = "CompanyHQ",
            Version = "1.0.0"
        })
            .WithHttpTransport(httpOptions => httpOptions.Stateless = true)
            .WithTools(McpActionRegistry.CreateTools(actions, mvc.PartManager))
            .WithRequestFilters(filters => filters.AddListToolsFilter(next => async (context, cancellationToken) =>
            {
                var result = await next(context, cancellationToken);

                result.Tools = [.. result.Tools.Where(tool => McpAccess.IsAllowed(context.User, tool.Name))];
                return result;
            }).AddCallToolFilter(next => async (context, cancellationToken) =>
            {
                McpAccess.RequireCapability(context.User, context.Params.Name);
                return await next(context, cancellationToken);
            }));

        // Dynamic client registration is anonymous by design, so the caller's address is the only
        // handle for bounding how many clients one source can register.
        services.AddRateLimiter(rateLimiter => rateLimiter.AddPolicy(
            McpOAuthRateLimits.ClientRegistration,
            context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 20,
                    Window = TimeSpan.FromHours(1),
                    QueueLimit = 0,
                    AutoReplenishment = true
                })));

        return services;
    }
}
