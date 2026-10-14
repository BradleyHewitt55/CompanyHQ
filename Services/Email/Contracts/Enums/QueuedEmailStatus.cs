namespace CompanyHQ.Email.Contracts.Enums;

/// <summary>Lifecycle state of one persisted outbound email.</summary>
public enum QueuedEmailStatus
{
    Pending,
    Sent,
    Failed,
    Abandoned
}
