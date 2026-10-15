using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Ports;

namespace CompanyHQ.Messages.Services;

/// <summary>Shared conversation history must be explicitly removed before an account disappears.</summary>
/// <remarks>
/// This is a data-integrity check run by account deletion, not a Messages operation, so it is never
/// admission-gated: conversation rows survive while Messages is switched off and still block deletion,
/// and a disabled Messages must not stop every account from being deleted.
/// </remarks>
public sealed class MessageAccountDeletionGuard(IServiceAvailability? availability, IPersistence context) : IAccountDeletionGuard
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public MessageAccountDeletionGuard(IPersistence context) : this(null, context) { }

    // Kept so the 3.1.1 public constructor stays source and binary compatible; deliberately unused.
    private readonly IServiceAvailability? unusedAvailability = availability;

    public async Task<string?> InspectAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Tracked<ConversationParticipant>().AnyAsync(row => row.UserId == userId, cancellationToken)
            ? "Delete your conversations before deleting your account."
            : null;

    public Task ReleaseAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
