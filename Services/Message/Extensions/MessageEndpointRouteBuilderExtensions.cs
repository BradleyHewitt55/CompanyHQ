using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

using CompanyHQ.Services.Messages.Hubs;

namespace CompanyHQ.Services.Messages.Extensions;

public static class MessageEndpointRouteBuilderExtensions
{
    /// <summary>Maps the receive-only delivery hub. Controllers are mapped by the host's <c>MapControllers</c>.</summary>
    public static IEndpointRouteBuilder MapMessages(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapHub<MessageHub>(MessageHub.Path);

        return endpoints;
    }
}
