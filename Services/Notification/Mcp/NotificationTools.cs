using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Notifications.Contracts.Dtos;
using CompanyHQ.Notifications.Services;
using CompanyHQ.Auth.Contracts.Identity;

namespace CompanyHQ.Notifications.Mcp;

[McpServerToolType]
public sealed class NotificationTools(INotificationAccountService notifications, IHttpContextAccessor httpContextAccessor)
{
    [McpServerTool(Name = "get_my_notification_preferences")]
    public async Task<NotificationPreferencesDto> GetMyNotificationPreferences(CancellationToken cancellationToken = default)
    {
        var userId = httpContextAccessor.HttpContext?.User.GetUserId()
            ?? throw new InvalidOperationException("No authenticated account.");

        return await notifications.GetPreferencesAsync(userId, cancellationToken);
    }
}
