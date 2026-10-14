namespace CompanyHQ.Notifications.Contracts.Dtos;

/// <summary>Small delivery-health summary for the existing administrator dashboard.</summary>
public sealed record AdminNotificationQueueHealthDto(int Pending, int Failed, int Abandoned);
