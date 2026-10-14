using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CompanyHQ.Postgres.Extensions;

/// <summary>Safe, provider-specific classification kept inside the Postgres service boundary.</summary>
public static class PostgresExceptionExtensions
{
    public static bool IsUniqueConstraintViolation(
        this DbUpdateException exception,
        string constraintName)
    {
        ArgumentNullException.ThrowIfNull(exception);
        ArgumentException.ThrowIfNullOrWhiteSpace(constraintName);

        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is PostgresException
                {
                    SqlState: "23505",
                    ConstraintName: var actualConstraint
                }
                && string.Equals(actualConstraint, constraintName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
