namespace CompanyHQ.Notifications.Contracts.Dtos;

/// <summary>Effective notification preferences for one user.</summary>
public sealed record NotificationPreferencesDto(bool EmailEnabled, bool PushEnabled);
