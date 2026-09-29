using CompanyHQ.Services.Notifications.Contracts.Enums;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Notifications.Contracts.Models;

/// <summary>A user's FCM registration token, one row per device installation.</summary>
public sealed class PushDevice : BaseModel
{
    public Guid UserId { get; set; }

    public required string Token { get; set; }

    public DevicePlatform Platform { get; set; }

    public DateTimeOffset LastSeenAt { get; set; }

    public DateTimeOffset? DisabledAt { get; set; }
}
