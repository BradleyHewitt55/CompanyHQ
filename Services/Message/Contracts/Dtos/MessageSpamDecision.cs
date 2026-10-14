namespace CompanyHQ.Messages.Contracts.Dtos;

public sealed record MessageSpamDecision(
    bool IsAvailable,
    bool ShouldFlag,
    long VolumeCount,
    long SpreadCount,
    TimeSpan Window)
{
    public static MessageSpamDecision Unavailable(TimeSpan window) =>
        new(false, false, 0, 0, window);
}
