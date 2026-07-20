namespace Abstractions.Data;

public sealed class UniqueConstraintException(string message, Exception inner)
    : Exception(message, inner);
