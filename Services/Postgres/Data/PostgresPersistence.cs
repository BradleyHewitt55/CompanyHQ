using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using CompanyHQ.Services.Postgres.Contracts.Models;
using CompanyHQ.Services.Persistence.Services;

namespace CompanyHQ.Services.Postgres.Data;

/// <summary>
/// <see cref="IPersistence"/> over the shared <see cref="PostgresDbContext"/>. It is the same scoped
/// context <see cref="IRepository{TModel}"/> and <c>IPostgresService</c> use, so whatever they
/// stage, this saves, and the other way round.
/// </summary>
internal sealed class PostgresPersistence(PostgresDbContext context) : IPersistence
{
    public IQuery<TModel> Query<TModel>() where TModel : BaseModel =>
        new PostgresQuery<TModel>(context.Set<TModel>().AsNoTracking());

    public IQuery<TModel> Tracked<TModel>() where TModel : BaseModel =>
        new PostgresQuery<TModel>(context.Set<TModel>());

    public IQuery<TModel> Locked<TModel>(bool skipLocked = false) where TModel : BaseModel =>
        new PostgresQuery<TModel>(context.Set<TModel>()
            .TagWith(skipLocked ? PostgresRowLockInterceptor.SkipLockedTag : PostgresRowLockInterceptor.WaitTag));

    public Task<TModel?> FindAsync<TModel>(Guid id, CancellationToken cancellationToken = default) where TModel : BaseModel =>
        context.Set<TModel>().FindAsync([id], cancellationToken).AsTask();

    public void Add<TModel>(TModel model) where TModel : BaseModel => context.Set<TModel>().Add(model);

    public void AddRange<TModel>(IEnumerable<TModel> models) where TModel : BaseModel => context.Set<TModel>().AddRange(models);

    public void Remove<TModel>(TModel model) where TModel : BaseModel => context.Set<TModel>().Remove(model);

    public void RemoveRange<TModel>(IEnumerable<TModel> models) where TModel : BaseModel => context.Set<TModel>().RemoveRange(models);

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        PostgresFailure.GuardAsync(() => context.SaveChangesAsync(cancellationToken));

    public void Clear() => context.ChangeTracker.Clear();

    public async Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        IsolationLevel? isolation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        // Called from inside another unit's transaction: it is part of that unit. The outer one
        // owns the commit, the retry, and the isolation level.
        if (context.Database.CurrentTransaction is not null)
        {
            return await work(cancellationToken);
        }

        try
        {
            // The host configures a retrying strategy, which refuses a transaction opened outside
            // it. Opening it inside makes the whole unit the thing that is retried.
            return await context.Database.CreateExecutionStrategy().ExecuteAsync(
                async token =>
                {
                    // A retry must not reuse what the failed attempt left tracked.
                    context.ChangeTracker.Clear();

                    await using var transaction = isolation is { } level
                        ? await context.Database.BeginTransactionAsync(level, token)
                        : await context.Database.BeginTransactionAsync(token);

                    var result = await work(token);
                    await PostgresFailure.GuardAsync(async () =>
                    {
                        await transaction.CommitAsync(token);

                        return 0;
                    });

                    return result;
                },
                cancellationToken);
        }
        catch (RetryLimitExceededException exception)
        {
            throw new PersistenceUnavailableException("The store could not complete the transaction after retrying.", exception);
        }
    }

    public Task InTransactionAsync(
        Func<CancellationToken, Task> work,
        IsolationLevel? isolation = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(work);

        return InTransactionAsync(
            async token =>
            {
                await work(token);

                return true;
            },
            isolation,
            cancellationToken);
    }
}

/// <summary>
/// Appends the row-lock clause to a query that <see cref="IPersistence.Locked{TModel}"/> tagged.
/// EF Core has no operator for <c>FOR UPDATE</c>, so the tag marks the command and this adds the
/// clause where PostgreSQL expects it: at the very end of the statement.
/// </summary>
internal sealed class PostgresRowLockInterceptor : DbCommandInterceptor
{
    public const string WaitTag = "companyhq:lock-rows";
    public const string SkipLockedTag = "companyhq:lock-rows-skip-locked";

    public override InterceptionResult<DbDataReader> ReaderExecuting(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result)
    {
        Lock(command, eventData);

        return base.ReaderExecuting(command, eventData, result);
    }

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
        DbCommand command,
        CommandEventData eventData,
        InterceptionResult<DbDataReader> result,
        CancellationToken cancellationToken = default)
    {
        Lock(command, eventData);

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    private static void Lock(DbCommand command, CommandEventData eventData)
    {
        // EF Core writes each tag as a leading comment line. The longer tag is tested first
        // because the shorter one is its prefix.
        var clause = command.CommandText.StartsWith($"-- {SkipLockedTag}", StringComparison.Ordinal) ? " FOR UPDATE SKIP LOCKED"
            : command.CommandText.StartsWith($"-- {WaitTag}", StringComparison.Ordinal) ? " FOR UPDATE"
            : null;

        if (clause is null)
        {
            return;
        }

        // Outside a transaction the lock is released the moment the statement ends, which would
        // look like protection and be none.
        if (eventData.Context?.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException(
                $"A locked query holds its rows until a transaction ends, so it must run inside {nameof(IPersistence)}.{nameof(IPersistence.InTransactionAsync)}.");
        }

        command.CommandText += clause;
    }
}

/// <summary>
/// Reports a write PostgreSQL refused as the provider-neutral failure a service can catch, so no
/// service has to know an EF Core or Npgsql exception type.
/// </summary>
internal static class PostgresFailure
{
    private const string UniqueViolation = "23505";

    public static async Task<T> GuardAsync<T>(Func<Task<T>> write)
    {
        try
        {
            return await write();
        }
        catch (Exception exception) when (Translate(exception) is { } translated)
        {
            throw translated;
        }
    }

    /// <summary>
    /// Null leaves the exception as it is. A transient failure is deliberately left alone: the
    /// retrying strategy recognises it by its own type, and wrapping it would turn a retry into
    /// an error.
    /// </summary>
    public static Exception? Translate(Exception exception)
    {
        if (exception is RetryLimitExceededException)
        {
            return new PersistenceUnavailableException("The store could not complete the write after retrying.", exception);
        }

        if (exception is DbUpdateConcurrencyException)
        {
            return new PersistenceConcurrencyException(exception);
        }

        if (exception is not (DbUpdateException or PostgresException))
        {
            return null;
        }

        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is PostgresException { SqlState: UniqueViolation } unique)
            {
                return new UniqueConstraintException(unique.ConstraintName, exception);
            }

            if (current is NpgsqlException { IsTransient: true })
            {
                return null;
            }
        }

        return new PersistenceException("The store rejected the write.", exception);
    }
}
