namespace CompanyHQ.Email.Contracts.Ports;

/// <summary>The provider result. A retryable failure was not accepted after the provider's local retries.</summary>
public readonly record struct EmailSendResult(string? EmailId, bool IsRetryable, string? Error)
{
    public bool IsSuccess => EmailId is not null;

    public static EmailSendResult Sent(string emailId) => new(emailId, false, null);

    public static EmailSendResult Failed(bool isRetryable, string error) => new(null, isRetryable, error);
}
