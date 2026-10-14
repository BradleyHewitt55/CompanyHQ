using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Ports;

namespace CompanyHQ.Messages.Services;

/// <summary>Shared conversation history must be explicitly removed before an account disappears.</summary>
public sealed class MessageAccountDeletionGuard(IPersistence context) : IAccountDeletionGuard
{
    public async Task<string?> InspectAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Tracked<ConversationParticipant>().AnyAsync(row => row.UserId == userId, cancellationToken)
            ? "Delete your conversations before deleting your account."
            : null;

    public Task ReleaseAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
