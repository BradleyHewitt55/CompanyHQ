using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Notifications.Contracts.Models;

/// <summary>Opt-out preferences for one user. Missing rows mean both channels are enabled.</summary>
public sealed class NotificationPreference : BaseModel
{
    public Guid UserId { get; set; }

    public bool EmailEnabled { get; set; } = true;

    public bool PushEnabled { get; set; } = true;
}
