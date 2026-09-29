using System.ComponentModel;

using Microsoft.AspNetCore.Http;

using ModelContextProtocol.Server;

using CompanyHQ.Services.Notifications.Contracts.Dtos;
using CompanyHQ.Services.Notifications.Services;
using CompanyHQ.Services.Auth.Contracts.Identity;

namespace CompanyHQ.Services.Notifications.Mcp;

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
