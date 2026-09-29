using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using CompanyHQ.Services.Email.Contracts.Models;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Postgres.Services;

namespace CompanyHQ.Services.Email.Services;

/// <summary>
/// Validates a message and stores it for <see cref="EmailWorker"/>. Nothing here calls the provider,
/// so success means the email is durably queued, not delivered.
/// </summary>
public sealed class EmailService(IServiceScopeFactory scopeFactory, TimeProvider time) : IEmailService
{
    public async Task<EmailSendResult> SendAsync(
        EmailMessage message,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();

        // Its own scope: saving on the caller's context would flush whatever else that context tracks.
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IPostgresService<QueuedEmail>>();

        // The same business event queues once, which also absorbs an at-least-once caller's repeat.
        if (await QueuedIdAsync(queue, message.IdempotencyKey, cancellationToken) is { } existingId)
        {
            return EmailSendResult.Sent(existingId.ToString());
        }

        var queued = new QueuedEmail
        {
            To = [.. message.To],
            Subject = message.Subject,
            Html = message.Html,
            Text = message.Text,
            IdempotencyKey = message.IdempotencyKey,
            IsPriority = message.IsPriority,
            NextAttemptAt = time.GetUtcNow()
        };

        try
        {
            await queue.AddAsync(queued, cancellationToken);
            return EmailSendResult.Sent(queued.Id.ToString());
        }
        catch (DbUpdateException)
        {
            // A concurrent send of the same event won the unique index; theirs is the queued row.
            return await QueuedIdAsync(queue, message.IdempotencyKey, cancellationToken) is { } racedId
                ? EmailSendResult.Sent(racedId.ToString())
                : throw new InvalidOperationException("The email could not be queued.");
        }
    }

    private static Task<Guid?> QueuedIdAsync(
        IPostgresService<QueuedEmail> queue,
        string idempotencyKey,
        CancellationToken cancellationToken) =>
        queue.Query()
            .Where(email => email.IdempotencyKey == idempotencyKey)
            .Select(email => (Guid?)email.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
