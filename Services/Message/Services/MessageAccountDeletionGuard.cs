using CompanyHQ.ServiceCatalog.Contracts;
using CompanyHQ.Messages.Contracts.Models;
using CompanyHQ.Persistence.Services;
using CompanyHQ.Users.Contracts.Ports;

namespace CompanyHQ.Messages.Services;

/// <summary>Shared conversation history must be explicitly removed before an account disappears.</summary>
public sealed class MessageAccountDeletionGuard(IServiceAvailability? availability, IPersistence context) : IAccountDeletionGuard
{
    /// <summary>Retains the original strict constructor for existing consumers.</summary>
    public MessageAccountDeletionGuard(IPersistence context) : this(null, context) { }

    public async Task<string?> InspectAsync(Guid userId, CancellationToken cancellationToken = default) {
        await (availability?.RequireAsync("messages", false, cancellationToken) ?? Task.CompletedTask);
        return await context.Tracked<ConversationParticipant>().AnyAsync(row => row.UserId == userId, cancellationToken)
            ? "Delete your conversations before deleting your account."
            : null;
    }

    public Task ReleaseAsync(Guid userId, CancellationToken cancellationToken = default) {
        availability?.RequireAsync("messages", false, cancellationToken).GetAwaiter().GetResult();
        return Task.CompletedTask;
    }
}
