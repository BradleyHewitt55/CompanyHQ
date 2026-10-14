namespace CompanyHQ.Auth.Contracts.Models;

/// <summary>Bounded maintenance settings for old refresh-token rows.</summary>
public sealed class RefreshTokenCleanupOptions
{
    public const string SectionName = "Auth:RefreshTokenCleanup";

    public int IntervalMinutes { get; set; } = 15;

    public int BatchSize { get; set; } = 500;

    /// <summary>Keep revoked rows long enough for reuse investigation before deleting them.</summary>
    public int RevokedRetentionDays { get; set; } = 30;

    public TimeSpan Interval => TimeSpan.FromMinutes(IntervalMinutes);

    public TimeSpan RevokedRetention => TimeSpan.FromDays(RevokedRetentionDays);

    public void Validate()
    {
        if (IntervalMinutes <= 0 || BatchSize <= 0 || RevokedRetentionDays <= 0)
        {
            throw new InvalidOperationException(
                $"{SectionName}:IntervalMinutes, BatchSize, and RevokedRetentionDays must be greater than zero.");
        }
    }
}
