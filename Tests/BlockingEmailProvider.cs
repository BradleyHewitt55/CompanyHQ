using System.Collections.Concurrent;
using CompanyHQ.Services.Email.Contracts.Ports;

namespace CompanyHQ.Tests;

internal sealed class BlockingEmailProvider : IEmailProvider
{
    private int calls;
    public ConcurrentBag<string> Keys { get; } = [];
    public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        Keys.Add(message.IdempotencyKey);
        if (Interlocked.Increment(ref calls) == 1)
        {
            Started.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
        return EmailSendResult.Sent("fixture-" + message.IdempotencyKey);
    }
}
