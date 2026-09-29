namespace CompanyHQ.Services.Messages.Contracts.Ports;

/// <summary>
/// Whether an uploaded object may be attached to a message: ready, and owned by the sender.
/// Optional: with no adapter plugged in, a send carrying an attachment is invalid.
/// </summary>
public interface IMessageAttachments
{
    Task<bool> IsReadyAsync(
        Guid mediaObjectId,
        Guid ownerUserId,
        CancellationToken cancellationToken = default);
}
