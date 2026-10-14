namespace CompanyHQ.Messages.Contracts.Ports;

/// <summary>
/// Whether an uploaded object may be attached to a message (ready, and owned by the sender), and
/// its removal when the conversation holding it is deleted.
/// Optional: with no adapter plugged in, a send carrying an attachment is invalid.
/// </summary>
public interface IMessageAttachments
{
    Task<bool> IsReadyAsync(
        Guid mediaObjectId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the stored objects of a conversation that no longer exists.</summary>
    Task DeleteAsync(
        IReadOnlyCollection<Guid> mediaObjectIds,
        Guid actingUserId,
        CancellationToken cancellationToken = default);
}
