namespace CompanyHQ.Messages.Contracts;

public enum MessageOperationStatus
{
    Success,
    NotFound,
    Invalid,
    Conflict,
    Forbidden
}

public sealed record MessageOperationResult<T>(
    MessageOperationStatus Status,
    T? Value = default)
{
    public bool Succeeded => Status is MessageOperationStatus.Success;

    public static MessageOperationResult<T> Success(T value) =>
        new(MessageOperationStatus.Success, value);

    public static MessageOperationResult<T> NotFound() =>
        new(MessageOperationStatus.NotFound);

    public static MessageOperationResult<T> Invalid() =>
        new(MessageOperationStatus.Invalid);

    public static MessageOperationResult<T> Conflict() =>
        new(MessageOperationStatus.Conflict);

    public static MessageOperationResult<T> Forbidden() =>
        new(MessageOperationStatus.Forbidden);
}
