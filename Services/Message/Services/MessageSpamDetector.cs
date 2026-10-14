using CompanyHQ.Cache.Contracts.Models;
using CompanyHQ.Cache.Services;
using CompanyHQ.Messages.Contracts.Dtos;
using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Messages.Contracts.Ports;

namespace CompanyHQ.Messages.Services;

/// <summary>Counts message volume and distinct first-contact threads without a Postgres aggregate.</summary>
internal sealed class MessageSpamDetector(
    ICacheService cache,
    MessageSpamOptions options)
{
    public async Task<MessageSpamDecision> RecordAsync(
        Guid userId,
        Guid conversationId,
        bool newConversation,
        CancellationToken cancellationToken = default)
    {
        var window = TimeSpan.FromMinutes(options.WindowMinutes);
        var volume = await cache.IncrementAsync(
            VolumeKey(userId),
            window,
            cancellationToken);

        var spread = new CacheCounterResult(true, 0, TimeSpan.Zero);
        if (newConversation)
        {
            spread = await cache.IncrementAsync(
                SpreadKey(userId),
                window,
                cancellationToken);
        }

        if (!volume.IsAvailable || !spread.IsAvailable)
        {
            // A security counter that cannot be read must not silently permit unbounded sends.
            return MessageSpamDecision.Unavailable(window);
        }

        return new MessageSpamDecision(
            true,
            volume.Count > options.VolumeLimit || spread.Count > options.SpreadLimit,
            volume.Count,
            spread.Count,
            window);
    }

    public string BuildEvidence(
        MessageSpamDecision decision,
        IReadOnlyList<MessageCapture> captures)
    {
        var sampleSize = options.EvidenceSampleSize;
        var sample = string.Join(
            " || ",
            captures
                .Take(sampleSize)
                .Select(capture => $"{capture.AuthorUserId:D}:{capture.Body}"));
        var evidence = $"volume={decision.VolumeCount};spread={decision.SpreadCount};window={decision.Window.TotalMinutes:0}m";
        return string.IsNullOrEmpty(sample)
            ? evidence
            : $"{evidence};sample={sample}"[..Math.Min(4000, evidence.Length + 8 + sample.Length)];
    }

    private static string VolumeKey(Guid userId) => $"message:spam:volume:{userId:D}";

    private static string SpreadKey(Guid userId) => $"message:spam:spread:{userId:D}";
}
