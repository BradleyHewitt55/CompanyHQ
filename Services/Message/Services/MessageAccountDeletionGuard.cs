using CompanyHQ.Services.Messages.Contracts.Models;
using CompanyHQ.Services.Persistence.Services;
using CompanyHQ.Services.Users.Contracts.Ports;

namespace CompanyHQ.Services.Messages.Services;

/// <summary>Shared conversation history must be explicitly removed before an account disappears.</summary>
public sealed class MessageAccountDeletionGuard(IPersistence context) : IAccountDeletionGuard
{
    public async Task<string?> InspectAsync(Guid userId, CancellationToken cancellationToken = default) =>
        await context.Tracked<ConversationParticipant>().AnyAsync(row => row.UserId == userId, cancellationToken)
            ? "Delete your conversations before deleting your account."
            : null;

    public Task ReleaseAsync(Guid userId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
