namespace CompanyHQ.Services.Persistence.Services;

/// <summary>
/// A write the store refused because of the data: a constraint it enforces was violated, or the row
/// it was asked to change is gone. A failed read, a caller's bug, and an unreachable store are
/// never reported as this.
/// </summary>
public class PersistenceException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// The store could not be reached, or gave up after its own retries. Nothing is known about the
/// write. It is deliberately not a <see cref="PersistenceException"/>, so a handler written for a
/// lost race never fires because the store went away.
/// </summary>
public sealed class PersistenceUnavailableException(string message, Exception? innerException = null)
    : Exception(message, innerException);

/// <summary>
/// A write lost a race for a unique key. Services catch this to turn "someone else got there first"
/// into an ordinary answer instead of checking first and hoping.
/// </summary>
public sealed class UniqueConstraintException(string? constraintName, Exception? innerException = null)
    : PersistenceException("A unique constraint was violated.", innerException)
{
    /// <summary>
    /// The violated constraint as the owning service named it in its model configuration, when the
    /// store reports one.
    /// </summary>
    public string? ConstraintName { get; } = constraintName;

    /// <summary>Whether this is the violation of one particular constraint. A service that owns several unique keys asks for the one it expects.</summary>
    public bool Is(string constraintName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(constraintName);

        return string.Equals(ConstraintName, constraintName, StringComparison.Ordinal);
    }
}

/// <summary>A tracked update or delete lost a race: the row or its concurrency version changed.</summary>
public sealed class PersistenceConcurrencyException(Exception? innerException = null)
    : PersistenceException("The persisted row changed before this write could complete.", innerException);

/// <summary>Thrown when a caller names a search field their role's shape does not carry.</summary>
public class SearchFieldException(string field, IReadOnlyList<string> allowed)
    : ArgumentException($"'{field}' is not a searchable field. Use one of: {string.Join(", ", allowed)}.")
{
    public string Field { get; } = field;

    /// <summary>The fields this caller may search, as they are spelled on the wire.</summary>
    public IReadOnlyList<string> Allowed { get; } = allowed;
}
