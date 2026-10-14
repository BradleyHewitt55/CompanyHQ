using Microsoft.Extensions.Logging;
using CompanyHQ.Email.Contracts.Enums;
using CompanyHQ.Email.Contracts.Models;
using CompanyHQ.Email.Contracts.Ports;
using CompanyHQ.Persistence.Services;

namespace CompanyHQ.Email.Services;

/// <summary>
/// Claims one due batch of <c>queued_email</c> and sends it through the active
/// <see cref="IEmailProvider"/>, priority rows first. Scoped, and resolved per batch by
/// <see cref="EmailWorker"/>: taking the provider in the constructor is what lets the host's
/// build-time validation refuse to start when the host has not registered the Resend client.
/// </summary>
internal sealed class EmailQueueProcessor(
    IPersistence context,
    IEmailProvider provider,
    TimeProvider timeProvider,
    ILogger<EmailQueueProcessor> logger)
{
    // About one second of sends at the provider's default pace, so a priority email that arrives
    // while a batch is in flight waits at most that long before the next claim picks it first.
    private const int BatchSize = 10;
    private const int MaxAttempts = 5;

    // Comfortably longer than a batch can take at the provider's request timeout, so a live worker
    // never loses its claim; an expired lease makes a crashed worker's rows eligible again.
    private static readonly TimeSpan ClaimLease = TimeSpan.FromMinutes(10);

    /// <summary>Claims and sends one small due batch.</summary>
    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();

        // Same shape as NotificationWorker: the claim is one transaction, and rows another worker
        // has already locked are skipped rather than waited for, so two workers never send the
        // same email.
        var emails = await context.InTransactionAsync(async _ =>
        {
            var claimed = await context.Locked<QueuedEmail>(skipLocked: true)
                .Where(email => email.Status == QueuedEmailStatus.Pending && email.NextAttemptAt <= now)
                .OrderByDescending(email => email.IsPriority)
                .ThenBy(email => email.NextAttemptAt)
                .ThenBy(email => email.Id)
                .Take(BatchSize)
                .ToListAsync(cancellationToken);

            foreach (var email in claimed)
            {
                email.NextAttemptAt = now.Add(ClaimLease);
            }

            await context.SaveChangesAsync(cancellationToken);
            return claimed;
        }, cancellationToken: cancellationToken);

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
