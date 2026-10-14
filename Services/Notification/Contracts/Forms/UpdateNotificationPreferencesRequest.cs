namespace CompanyHQ.Notifications.Contracts.Forms;

/// <summary>Replaces the two channel opt-out switches for the authenticated account.</summary>
public sealed record UpdateNotificationPreferencesRequest(
    bool EmailEnabled,
    bool PushEnabled);
