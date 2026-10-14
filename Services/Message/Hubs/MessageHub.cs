using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Messages.Hubs;

/// <summary>Receive-only delivery hub. HTTP is the only message write path.</summary>
[Authorize]
public sealed class MessageHub : Hub
{
    /// <summary>Under <see cref="AuthHubs.PathPrefix"/>, so a browser can authenticate the socket.</summary>
    public const string Path = AuthHubs.PathPrefix + "/messages";

    public static string UserGroup(Guid userId) => $"user:{userId:D}";

    public override async Task OnConnectedAsync()
    {
        if (!Guid.TryParse(Context.User?.FindFirst(AuthClaims.Subject)?.Value, out var userId)
            || userId == Guid.Empty)
        {
            Context.Abort();
            return;
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, UserGroup(userId));
        await base.OnConnectedAsync();
    }
}
