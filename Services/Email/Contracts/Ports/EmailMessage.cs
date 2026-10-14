namespace CompanyHQ.Email.Contracts.Ports;

/// <summary>
/// A transactional message. The caller owns the stable idempotency key for its business event.
/// A priority message is sent ahead of every waiting normal one.
/// </summary>
public sealed record EmailMessage(
    IReadOnlyList<string> To,
    string Subject,
    string Html,
    string Text,
    string IdempotencyKey,
    bool IsPriority = false)
{
    /// <summary>Throws when the message could not be sent by any provider.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(To);

        if (To.Count is < 1 or > 50 || To.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("An email must have between one and 50 recipients.", nameof(To));
        }

        if (string.IsNullOrWhiteSpace(Subject) || Subject.Length > 255)
        {
            throw new ArgumentException("Email subject is required and must be at most 255 characters.", nameof(Subject));
        }

        if (string.IsNullOrWhiteSpace(Html) || string.IsNullOrWhiteSpace(Text))
        {
            throw new ArgumentException("Transactional email must include both HTML and plain-text bodies.");
        }

        if (string.IsNullOrWhiteSpace(IdempotencyKey) || IdempotencyKey.Length > 256)
        {
            throw new ArgumentException("Idempotency key is required and must be at most 256 characters.", nameof(IdempotencyKey));
        }
    }
}
