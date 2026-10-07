using CompanyHQ.Services.Media.Contracts.Enums;
using CompanyHQ.Services.Media.Contracts.Ports;
using CompanyHQ.Services.Media.Services;
using CompanyHQ.Services.Messages.Contracts.Ports;
using CompanyHQ.Services.Messages.Services;
using CompanyHQ.Services.Moderation.Contracts.Captures;
using CompanyHQ.Services.Moderation.Contracts.Enums;
using CompanyHQ.Services.Moderation.Contracts.Ports;
using CompanyHQ.Services.Notifications.Contracts.Enums;
using CompanyHQ.Services.Notifications.Contracts.Ports;

namespace Api.Integrations;

/// <summary>Fills Message's block, flag, and restriction ports from Moderation.</summary>
internal sealed class ModerationMessageSafety(
    IBlockChecker blocks,
    IAccountFlags flags,
    IContentRestrictions restrictions) : IMessageBlocks, IMessageAccountFlags, IMessageRestrictions
{
    public Task<bool> IsBlockedBetweenAsync(Guid a, Guid b, CancellationToken cancellationToken = default) =>
        blocks.IsBlockedBetweenAsync(a, b, cancellationToken);

    public Task<IReadOnlySet<Guid>> BlockedForAsync(Guid userId, CancellationToken cancellationToken = default) =>
        blocks.BlockedForAsync(userId, cancellationToken);

    public Task RaiseAsync(Guid userId, string kind, string evidence, CancellationToken cancellationToken = default) =>
        flags.RaiseAsync(userId, kind, evidence, cancellationToken);

    public Task<bool> IsFlaggedAsync(Guid userId, string kind, CancellationToken cancellationToken = default) =>
        flags.IsFlaggedAsync(userId, kind, cancellationToken);

    public Task<IReadOnlySet<Guid>> RestrictedMessageIdsAsync(CancellationToken cancellationToken = default) =>
        restrictions.RestrictedIdsAsync(MessageReportSource.Kind, cancellationToken);
}

/// <summary>Makes a message reportable in Moderation, with its neighbours as frozen evidence.</summary>
internal sealed class MessageReportSource(MessageContextReader reader) : IReportableContentSource
{
    public const string Kind = "message";

    public string TargetKind => Kind;

    public IReadOnlyList<ReportReason> AllowedReasons { get; } =
    [
        ReportReason.Spam,
        ReportReason.Harassment,
        ReportReason.Impersonation,
        ReportReason.SexualContent,
        ReportReason.Violence,
        ReportReason.Other
    ];

    public async Task<ReportableContentCaptureResult> CaptureAsync(Guid targetId, CancellationToken ct = default)
    {
        if (await reader.ReadAsync(targetId, ct) is not { } context)
        {
            return ReportableContentCaptureResult.NotFound;
        }

        return ReportableContentCaptureResult.ForFound(
            context.SenderUserId,
            context.Captures
                .Select(capture => new ReportedContentCapture(
                    capture.AuthorUserId,
                    capture.Body,
                    capture.OccurredAt,
                    capture.IsTarget))
                .ToArray());
    }
}

/// <summary>After a message is reported, hides that conversation from the reporter.</summary>
internal sealed class MessageReportHistoryClearer(MessageHistoryClearer clearer) : IReportHistoryClearer
{
    public string TargetKind => MessageReportSource.Kind;

    public Task ClearAsync(Guid reporterUserId, Guid targetId, CancellationToken ct = default) =>
        clearer.ClearForMessageAsync(reporterUserId, targetId, ct);
}

/// <summary>After an account is reported, hides every conversation the reporter shares with it.</summary>
internal sealed class UserReportMessageHistoryClearer(MessageHistoryClearer clearer) : IReportHistoryClearer
{
    public string TargetKind => "user";

    public Task ClearAsync(Guid reporterUserId, Guid targetId, CancellationToken ct = default) =>
        clearer.ClearWithUserAsync(reporterUserId, targetId, ct);
}

/// <summary>Media's read policy for message attachments.</summary>
internal sealed class MessageMediaAccessPolicy(MessageAttachmentAccess access) : IMediaAccessPolicy
{
    public MediaScope Scope => MediaScope.Message;

    public Task<bool> CanReadAsync(Guid mediaObjectId, Guid viewerUserId, CancellationToken cancellationToken = default) =>
        access.CanReadAsync(mediaObjectId, viewerUserId, cancellationToken);
}

/// <summary>
/// Message's check before attaching an upload, and the removal of a deleted conversation's files.
/// Kept apart from the read policy above: Media resolves every read policy to build itself, so a
/// policy that needed Media back would be a dependency cycle.
/// </summary>
internal sealed class MessageAttachments(
    IMediaAttachmentVerifier verifier,
    IMediaService media) : IMessageAttachments
{
    public Task<bool> IsReadyAsync(Guid mediaObjectId, Guid ownerUserId, CancellationToken cancellationToken = default) =>
        verifier.IsReadyAsync(mediaObjectId, ownerUserId, MediaScope.Message, cancellationToken);

    // Message has already authorised the delete, so the files go whoever uploaded them.
    public async Task DeleteAsync(IReadOnlyCollection<Guid> mediaObjectIds, Guid actingUserId, CancellationToken cancellationToken = default)
    {
        foreach (var id in mediaObjectIds)
        {
            await media.DeleteAsync(id, actingUserId, administrator: true, cancellationToken);
        }
    }
}

/// <summary>Queues Message's conversation alerts as push notifications.</summary>
internal sealed class MessagePushNotifier(INotificationService queue) : IMessageNotifier
{
    public Task EnqueueAsync(MessageNotification notification, CancellationToken cancellationToken = default) =>
        queue.EnqueueAsync(ToRequest(notification), cancellationToken);

    public Task EnqueueManyAsync(IEnumerable<MessageNotification> notifications, CancellationToken cancellationToken = default) =>
        queue.EnqueueManyAsync(notifications.Select(ToRequest), cancellationToken);

    private static NotificationRequest ToRequest(MessageNotification notification) =>
        new(
            notification.RecipientUserId,
            notification.Kind,
            notification.Title,
            notification.Body,
            notification.Data,
            NotificationChannels.Push);
}
