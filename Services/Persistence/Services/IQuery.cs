using System.Linq.Expressions;

namespace CompanyHQ.Services.Persistence.Services;

/// <summary>
/// A description of one read over persisted models: which rows, in what order, in what shape.
/// Nothing runs until a method ending in <c>Async</c> is awaited, and the whole description is
/// evaluated by the store, never in memory.
/// </summary>
/// <remarks>
/// <para>
/// The operators are a closed set, and they are all there is. A query cannot be cast to, or
/// extended with, anything belonging to a database driver or an object-relational mapper, and a
/// provider rejects an expression that names one. What is not here — a row lock, raw SQL, a
/// provider function — is asked for through a named capability or is not available.
/// </para>
/// <para>
/// Expressions may use comparison, boolean logic, null checks, arithmetic, string concatenation,
/// <c>Contains</c> on an in-memory collection, <see cref="QueryFunctions"/>, mapped properties and
/// navigations, and another query as a correlated subquery. A provider is free to refuse anything
/// else; it must refuse it by throwing, never by evaluating it in memory.
/// </para>
/// <para>
/// It describes a relational read. A store that cannot join or correlate cannot implement it, and
/// nothing here pretends otherwise.
/// </para>
/// </remarks>
public interface IQuery<T>
{
    IQuery<T> Where(Expression<Func<T, bool>> predicate);

    IQuery<TResult> Select<TResult>(Expression<Func<T, TResult>> selector);

    /// <summary>Pairs each row with the rows of <paramref name="inner"/> that share its key. Unmatched rows are dropped.</summary>
    IQuery<TResult> Join<TInner, TKey, TResult>(
        IQuery<TInner> inner,
        Expression<Func<T, TKey>> outerKey,
        Expression<Func<TInner, TKey>> innerKey,
        Expression<Func<T, TInner, TResult>> result);

    IOrderedQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key);

    IOrderedQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key);

    IQuery<T> Skip(int count);

    IQuery<T> Take(int count);

    IQuery<T> Distinct();

    /// <summary>
    /// Narrows to rows whose <paramref name="field"/> contains <paramref name="keyword"/>, without
    /// case. The searchable fields are exactly the properties of <paramref name="shape"/> — the
    /// response type the caller is about to receive — so a caller can search what they may read
    /// and nothing else. With no field, any text field of the shape may match. A blank keyword
    /// changes nothing.
    /// </summary>
    /// <param name="aliases">Shape property to model path, for a field the model spells differently or reaches through a navigation.</param>
    /// <exception cref="SearchFieldException">The field is not in the shape, or cannot be searched.</exception>
    IQuery<T> Search(string? field, string? keyword, Type shape, IReadOnlyDictionary<string, string>? aliases = null);

    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);

    Task<T[]> ToArrayAsync(CancellationToken cancellationToken = default);

    Task<HashSet<T>> ToHashSetAsync(CancellationToken cancellationToken = default);

    Task<Dictionary<TKey, TValue>> ToDictionaryAsync<TKey, TValue>(
        Func<T, TKey> key,
        Func<T, TValue> value,
        CancellationToken cancellationToken = default)
        where TKey : notnull;

    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">No row matched.</exception>
    Task<T> FirstAsync(CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">More than one row matched.</exception>
    Task<T?> SingleOrDefaultAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="SingleOrDefaultAsync(CancellationToken)"/>
    Task<T?> SingleOrDefaultAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <exception cref="InvalidOperationException">No row, or more than one, matched.</exception>
    Task<T> SingleAsync(CancellationToken cancellationToken = default);

    /// <inheritdoc cref="SingleAsync(CancellationToken)"/>
    Task<T> SingleAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(CancellationToken cancellationToken = default);

    Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    Task<int> CountAsync(CancellationToken cancellationToken = default);

    Task<int> CountAsync(Expression<Func<T, bool>> predicate, CancellationToken cancellationToken = default);

    /// <summary>How many rows share each value of <paramref name="key"/>, counted by the store.</summary>
    Task<Dictionary<TKey, int>> CountByAsync<TKey>(Expression<Func<T, TKey>> key, CancellationToken cancellationToken = default)
        where TKey : notnull;

    /// <summary>
    /// Deletes every matching row in one statement and returns how many went. The filter is the
    /// authorisation: put the owner check in it, never in a prior read. Nothing tracked is touched.
    /// </summary>
    Task<int> DeleteAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Changes every matching row in one statement and returns how many changed. It writes exactly
    /// the properties named and nothing else: <c>UpdatedAt</c> moves only if it is one of them.
    /// </summary>
    Task<int> UpdateAsync(Action<IUpdateSetters<T>> set, CancellationToken cancellationToken = default);

    /// <summary>
    /// Correlated form of <see cref="AnyAsync(CancellationToken)"/>, for use inside the expression of
    /// another query, where it becomes a subquery. Calling it directly throws.
    /// </summary>
    bool Any();

    /// <inheritdoc cref="Any()"/>
    bool Any(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="Any()"/>
    int Count();

    /// <inheritdoc cref="Any()"/>
    int Count(Expression<Func<T, bool>> predicate);

    /// <inheritdoc cref="Any()"/>
    T? FirstOrDefault();

    /// <inheritdoc cref="Any()"/>
    bool Contains(T value);
}

/// <summary>A query with a primary order, to which tie-breakers can be added and which can be paged.</summary>
public interface IOrderedQuery<T> : IQuery<T>
{
    IOrderedQuery<T> ThenBy<TKey>(Expression<Func<T, TKey>> key);

    IOrderedQuery<T> ThenByDescending<TKey>(Expression<Func<T, TKey>> key);

    /// <summary>
    /// One window of the ordered rows plus the total before windowing. Paging exists only on an
    /// ordered query because a store may return unordered rows in any order, which lets a row
    /// repeat on one page and vanish from the next. End the order with a unique key.
    /// </summary>
    Task<Page<T>> ToPageAsync(int skip, int take, CancellationToken cancellationToken = default);

    /// <summary>The same window, with each row shaped by the store so no model is materialised.</summary>
    Task<Page<TItem>> ToPageAsync<TItem>(
        int skip,
        int take,
        Expression<Func<T, TItem>> selector,
        CancellationToken cancellationToken = default);
}

/// <summary>The properties one set-based update writes.</summary>
public interface IUpdateSetters<T>
{
    /// <summary>Sets a property to the same value on every matching row.</summary>
    IUpdateSetters<T> Set<TProperty>(Expression<Func<T, TProperty>> property, TProperty value);

    /// <summary>Sets a property from the row's own current values, for example a counter plus one.</summary>
    IUpdateSetters<T> Set<TProperty>(Expression<Func<T, TProperty>> property, Expression<Func<T, TProperty>> value);
}

/// <summary>One window of an ordered result and the number of rows it was cut from.</summary>
public sealed record Page<TItem>(IReadOnlyList<TItem> Items, int Total);
