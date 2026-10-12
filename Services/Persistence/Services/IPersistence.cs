using System.Data;
using CompanyHQ.Services.Postgres.Contracts.Models;

namespace CompanyHQ.Services.Persistence.Services;

/// <summary>
/// One unit of work over every persisted model: reads, staged writes that commit together, and
/// transactions. It lives for one scope — a request, or one pass of a worker — and everything
/// injected in that scope shares it, <see cref="IRepository{TModel}"/> included.
/// </summary>
/// <remarks>
/// Use <see cref="IRepository{TModel}"/> for a service that reads and writes one model at a time.
/// Reach for this when several models must commit together, or a decision must be read and acted
/// on atomically.
/// </remarks>
public interface IPersistence
{
    /// <summary>A read whose results are snapshots: changing one changes nothing in the store.</summary>
    IQuery<TModel> Query<TModel>() where TModel : BaseModel;

    /// <summary>
    /// A read whose model results are tracked: changes made to them are written by the next
    /// <see cref="SaveChangesAsync"/>. A model already tracked in this unit of work is returned as
    /// that same instance, with whatever unsaved changes it carries.
    /// </summary>
    IQuery<TModel> Tracked<TModel>() where TModel : BaseModel;

    /// <summary>
    /// A tracked read that also locks every row it returns until the surrounding transaction ends,
    /// so a concurrent transaction waits for it. With <paramref name="skipLocked"/> rows another
    /// transaction already holds are left out instead of waited for, which is how several workers
    /// claim disjoint batches from one queue.
    /// </summary>
    /// <remarks>
    /// This is an explicit capability rather than part of <see cref="IQuery{T}"/>: pessimistic row
    /// locking is something a relational provider has and another store may not. It only means
    /// anything inside <see cref="InTransactionAsync{TResult}"/>, and only for a query over this one
    /// model that is filtered, ordered and limited — no join, projection or subquery.
    /// </remarks>
    IQuery<TModel> Locked<TModel>(bool skipLocked = false) where TModel : BaseModel;

    /// <summary>
    /// The model with this id, tracked. A model already tracked here, including one staged by
    /// <see cref="Add{TModel}"/> and not yet saved, is returned without asking the store.
    /// </summary>
    Task<TModel?> FindAsync<TModel>(Guid id, CancellationToken cancellationToken = default) where TModel : BaseModel;

    /// <summary>Stages an insert. Nothing is written until <see cref="SaveChangesAsync"/>.</summary>
    void Add<TModel>(TModel model) where TModel : BaseModel;

    /// <inheritdoc cref="Add{TModel}"/>
    void AddRange<TModel>(IEnumerable<TModel> models) where TModel : BaseModel;

    /// <summary>Stages a delete of a tracked model. Nothing is written until <see cref="SaveChangesAsync"/>.</summary>
    void Remove<TModel>(TModel model) where TModel : BaseModel;

    /// <inheritdoc cref="Remove{TModel}"/>
    void RemoveRange<TModel>(IEnumerable<TModel> models) where TModel : BaseModel;

    /// <summary>
    /// Writes every staged insert, tracked change and staged delete together, and returns how many
    /// rows were written. Ids left empty are assigned, and <c>CreatedAt</c> and <c>UpdatedAt</c>
    /// are stamped here, so they are the same whichever port the write came through.
    /// </summary>
    /// <exception cref="UniqueConstraintException">A unique key was already taken.</exception>
    /// <exception cref="PersistenceException">The store rejected the write for another reason.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forgets everything tracked and staged without writing it. Use it between independent
    /// operations in one long scope, so one cannot commit what another left behind.
    /// </summary>
    void Clear();

    /// <summary>
    /// Runs <paramref name="work"/> in one transaction: it commits when the work returns and rolls
    /// back when it throws.
    /// </summary>
    /// <remarks>
    /// <b>The work may run more than once.</b> A provider retries the whole unit after a transient
    /// failure, such as a lost connection or a serialization conflict, and forgets everything
    /// tracked before each attempt. Nothing captured outside the delegate may assume a single
    /// execution, and nothing outside the store — an email, a cache write, a file — belongs
    /// inside it.
    /// </remarks>
    /// <param name="isolation">Null for the store's default level.</param>
    Task<TResult> InTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> work,
        IsolationLevel? isolation = null,
        CancellationToken cancellationToken = default);

    /// <inheritdoc cref="InTransactionAsync{TResult}"/>
    Task InTransactionAsync(
        Func<CancellationToken, Task> work,
        IsolationLevel? isolation = null,
        CancellationToken cancellationToken = default);
}
