using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using CompanyHQ.Services.Email.Contracts.Enums;
using CompanyHQ.Services.Email.Contracts.Models;
using CompanyHQ.Services.Email.Contracts.Ports;
using CompanyHQ.Services.Postgres.Data;

namespace CompanyHQ.Services.Email.Services;

/// <summary>
/// Claims one due batch of <c>queued_email</c> and sends it through the active
/// <see cref="IEmailProvider"/>, priority rows first. Scoped, and resolved per batch by
/// <see cref="EmailWorker"/>: taking the provider in the constructor is what lets the host's
/// build-time validation refuse to start when no provider adapter is registered.
/// </summary>
internal sealed class EmailQueueProcessor(
    PostgresDbContext context,
    IEmailProvider provider,
    TimeProvider timeProvider,
    ILogger<EmailQueueProcessor> logger)
{
    // About one second of sends at the provider's default pace, so a priority email that arrives
    // while a batch is in flight waits at most that long before the next claim picks it first.
    private const int BatchSize = 10;
    private const int MaxAttempts = 5;

    // Comfortably longer than a batch can take with the provider's own retries, so a live worker
    // never loses its claim; an expired lease makes a crashed worker's rows eligible again.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(10);

    /// <summary>Claims and sends one small due batch.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Same shape as NotificationWorker: the retrying execution strategy only accepts a
        // user-initiated transaction when the whole claim runs inside it.
        var emails = await context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var claimed = await context.Set<QueuedEmail>()
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM queued_email
                    WHERE status = {nameof(QueuedEmailStatus.Pending)}
                      AND next_attempt_at <= {now}
                    ORDER BY is_priority DESC, next_attempt_at, id
                    LIMIT {BatchSize}
                    FOR UPDATE SKIP LOCKED
                    """)
                .ToListAsync(cancellationToken);

            foreach (var email in claimed)
            {
                email.NextAttemptAt = now.Add(ClaimLease);
            }

            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return claimed;
        });

        foreach (var email in emails)
        {
            Apply(email, await SendAsync(email, cancellationToken), timeProvider.GetUtcNow());

            // Saved per email so a crash mid-batch re-sends only the unfinished rows.
            await context.SaveChangesAsync(cancellationToken);
        }

        return emails.Count;
    }

    internal static void Apply(QueuedEmail email, EmailSendResult result, DateTimeOffset now)
    {
        if (result.IsSuccess)
        {
            email.Status = QueuedEmailStatus.Sent;
            email.ProviderMessageId = result.EmailId;
            email.LastError = null;
        }
        else
        {
            email.Attempts++;
            email.LastError = result.Error;
            email.Status = !result.IsRetryable
                ? QueuedEmailStatus.Failed
                : email.Attempts >= MaxAttempts
                    ? QueuedEmailStatus.Abandoned
                    : QueuedEmailStatus.Pending;
            email.NextAttemptAt = now.AddSeconds(Math.Min(300, Math.Pow(2, email.Attempts - 1)));
        }

        if (email.Status is not QueuedEmailStatus.Pending)
        {
            email.Html = string.Empty;
            email.Text = string.Empty;
        }
    }

    private async Task<EmailSendResult> SendAsync(QueuedEmail email, CancellationToken cancellationToken)
    {
        try
        {
            return await provider.SendAsync(
                new EmailMessage(email.To, email.Subject, email.Html, email.Text, email.IdempotencyKey, email.IsPriority),
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning("Email {EmailId} send threw {ExceptionType}.", email.Id, exception.GetType().Name);
            return EmailSendResult.Failed(true, "The email provider threw before returning a result.");
        }
    }
}
