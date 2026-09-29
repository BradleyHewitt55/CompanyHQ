namespace CompanyHQ.Services.Messages.Contracts.Models;

/// <summary>Cache-counter thresholds for automated message-spam flagging.</summary>
public sealed class MessageSpamOptions
{
    public const string SectionName = "Message:Spam";

    public int VolumeLimit { get; set; } = 100;

    public int SpreadLimit { get; set; } = 20;

    public int WindowMinutes { get; set; } = 60;

    public int EvidenceSampleSize { get; set; } = 5;

    public void Validate()
    {
        if (VolumeLimit < 1 || SpreadLimit < 1)
        {
            throw new InvalidOperationException("Message:Spam:VolumeLimit and SpreadLimit must be at least 1.");
        }

        if (WindowMinutes is < 1 or > 24 * 60)
        {
            throw new InvalidOperationException("Message:Spam:WindowMinutes must be between 1 and 1440.");
        }

        if (EvidenceSampleSize is < 1 or > 20)
        {
            throw new InvalidOperationException("Message:Spam:EvidenceSampleSize must be between 1 and 20.");
        }
    }
}
