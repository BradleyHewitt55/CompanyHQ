using CompanyHQ.Email.Contracts.Enums;
using CompanyHQ.Persistence.Contracts.Models;

namespace CompanyHQ.Email.Contracts.Models;

/// <summary>
/// A rendered transactional email waiting for, or finished with, the email worker. The bodies are
/// cleared once the row leaves <see cref="QueuedEmailStatus.Pending"/>, because they carry reset
/// links and temporary passwords.
/// </summary>
public sealed class QueuedEmail : BaseModel
{
    public required string[] To { get; set; }

    public required string Subject { get; set; }

    public required string Html { get; set; }

    public required string Text { get; set; }

    public required string IdempotencyKey { get; set; }

    public bool IsPriority { get; set; }

    public QueuedEmailStatus Status { get; set; } = QueuedEmailStatus.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public string? ProviderMessageId { get; set; }

    public string? LastError { get; set; }
}
